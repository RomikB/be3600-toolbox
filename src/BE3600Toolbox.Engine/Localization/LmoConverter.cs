using System.Buffers.Binary;
using System.Text;

namespace BE3600Toolbox.Engine.Localization;

public static class LmoConverter
{
    private const int MsgUnspec = 0;
    private const int MsgCtxt = 1;
    private const int MsgId = 2;
    private const int MsgIdPlural = 3;
    private const int MsgStr = 4;

    private sealed class MsgState
    {
        public int PluralNum;
        public string? Ctxt;
        public string? Id;
        public string? IdPlural;
        public byte[]?[] Val = new byte[]?[10];
        public int Cur = MsgUnspec;
        public uint? Key;

        public void Init(int pluralNum = 0)
        {
            PluralNum = pluralNum;
            Ctxt = null;
            Id = null;
            IdPlural = null;
            Val = new byte[]?[10];
            Cur = MsgUnspec;
            Key = null;
        }
    }

    private sealed class LmoEntry
    {
        public uint KeyId;
        public int Plural;
        public uint Offset;
        public uint Length;
        public byte[] Val = Array.Empty<byte>();
    }

    public static uint SuperFastHash(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return 0;

        uint hash = (uint)data.Length;
        int rem = data.Length & 3;
        int length = data.Length >> 2;
        int offset = 0;

        for (int i = 0; i < length; i++)
        {
            uint a = (uint)(data[offset] | (data[offset + 1] << 8));
            hash += a;
            uint b = (uint)(data[offset + 2] | (data[offset + 3] << 8));
            uint t = (b << 11) ^ hash;
            hash = (hash << 16) ^ t;
            hash += hash >> 11;
            offset += 4;
        }

        if (rem == 3)
        {
            uint a = (uint)(data[offset] | (data[offset + 1] << 8));
            hash += a;
            hash ^= hash << 16;
            hash ^= unchecked((uint)((sbyte)data[offset + 2] << 18));
            hash += hash >> 11;
        }
        else if (rem == 2)
        {
            uint a = (uint)(data[offset] | (data[offset + 1] << 8));
            hash += a;
            hash ^= hash << 11;
            hash += hash >> 17;
        }
        else if (rem == 1)
        {
            hash += unchecked((uint)(sbyte)data[offset]);
            hash ^= hash << 10;
            hash += hash >> 1;
        }

        hash ^= hash << 3;
        hash += hash >> 5;
        hash ^= hash << 4;
        hash += hash >> 17;
        hash ^= hash << 25;
        hash += hash >> 6;

        return hash;
    }

    public static byte[] ConvertPoResourceToLmo(string resourceName, bool skipDup = true)
    {
        var assembly = typeof(LmoConverter).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly '{assembly.FullName}'. Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
        return ConvertPoToLmo(stream, skipDup);
    }

    public static byte[] ConvertPoToLmo(Stream poStream, bool skipDup = true)
    {
        using var reader = new StreamReader(poStream, Encoding.UTF8);
        return ConvertPoToLmo(reader, skipDup);
    }

    public static byte[] ConvertPoToLmo(string poText, bool skipDup = true)
    {
        using var reader = new StringReader(poText);
        return ConvertPoToLmo(reader, skipDup);
    }

    public static byte[] ConvertPoToLmo(TextReader reader, bool skipDup = true)
    {
        var entries = new List<LmoEntry>();
        var seenKeys = new HashSet<uint>();
        var msg = new MsgState();
        msg.Init(-1);

        void AddEntry(uint keyId, int plural, byte[] val)
        {
            if (skipDup && seenKeys.Contains(keyId))
            {
                return;
            }

            seenKeys.Add(keyId);
            entries.Add(new LmoEntry
            {
                KeyId = keyId,
                Plural = plural,
                Val = val
            });
        }

        void CommitMsg()
        {
            var val0 = msg.Val[0];
            if (string.IsNullOrEmpty(msg.Id) && (val0 == null || val0.Length == 0))
            {
                return;
            }

            if (val0 == null || val0.Length == 0)
            {
                msg.Init();
                return;
            }

            if (msg.Key.HasValue)
            {
                var val = msg.Val[0]!;
                AddEntry(msg.Key.Value, 0, val);
            }
            else if (!string.IsNullOrEmpty(msg.Id) && msg.PluralNum >= 0)
            {
                for (int i = 0; i < msg.Val.Length; i++)
                {
                    var val = msg.Val[i];
                    if (val == null || val.Length == 0) continue;

                    string key;
                    if (!string.IsNullOrEmpty(msg.Ctxt) && !string.IsNullOrEmpty(msg.IdPlural))
                    {
                        key = $"{msg.Ctxt}\x01{msg.Id}\x02{i}";
                    }
                    else if (!string.IsNullOrEmpty(msg.Ctxt))
                    {
                        key = $"{msg.Ctxt}\x01{msg.Id}";
                    }
                    else if (!string.IsNullOrEmpty(msg.IdPlural))
                    {
                        key = $"{msg.Id}\x02{i}";
                    }
                    else
                    {
                        key = msg.Id;
                    }

                    uint keyId = SuperFastHash(Encoding.UTF8.GetBytes(key));
                    uint valId = SuperFastHash(val);
                    if (keyId != valId)
                    {
                        AddEntry(keyId, msg.PluralNum, val);
                    }
                }
            }
            else
            {
                var val = msg.Val[0]!;
                var valSpan = val.AsSpan();
                byte[] prefix = "\\nPlural-Forms: "u8.ToArray();
                int x = valSpan.IndexOf(prefix);
                if (x > 0)
                {
                    x += prefix.Length;
                    byte[] suffix = "\\n"u8.ToArray();
                    int x2 = valSpan[x..].IndexOf(suffix);
                    if (x2 > 0)
                    {
                        var slice = valSpan.Slice(x, x2).ToArray();
                        AddEntry(0, -1, slice);
                    }
                }
            }

            msg.Init();
        }

        string? ExtractString(string line)
        {
            if (line.StartsWith('#'))
            {
                return null;
            }

            int x = line.IndexOf('"');
            if (x < 0) return null;

            line = line[(x + 1)..];
            line = line.Replace(@"\\", "\x02");
            line = line.Replace(@"\""", "\x01");

            x = line.IndexOf('"');
            if (x >= 0)
            {
                line = line[..x];
            }

            line = line.Replace("\x01", "\"");
            line = line.Replace("\x02", "\\");
            return line;
        }

        string? currentLine;
        while ((currentLine = reader.ReadLine()) != null)
        {
            var line = currentLine.TrimEnd('\r', '\n');

            if (line.StartsWith("msgctxt \"", StringComparison.Ordinal))
            {
                CommitMsg();
                msg.Ctxt = "";
                msg.Cur = MsgCtxt;
            }
            else if (line.StartsWith("msgid \"", StringComparison.Ordinal))
            {
                CommitMsg();
                msg.Id = "";
                msg.Cur = MsgId;
            }
            else if (line.StartsWith("msgid 0x", StringComparison.Ordinal) || line.StartsWith("msgkey 0x", StringComparison.Ordinal))
            {
                CommitMsg();
                msg.Id = "\x01";
                msg.PluralNum = 0;
                int x = line.IndexOf("0x", StringComparison.Ordinal);
                msg.Key = System.Convert.ToUInt32(line[x..], 16);
                msg.Cur = MsgUnspec;
            }
            else if (line.StartsWith("msgid_plural \"", StringComparison.Ordinal))
            {
                msg.IdPlural = "";
                msg.Cur = MsgIdPlural;
            }
            else if (line.StartsWith("msgstr \"", StringComparison.Ordinal) || line.StartsWith("msgstr[", StringComparison.Ordinal))
            {
                msg.PluralNum = 0;
                if (line.StartsWith("msgstr[", StringComparison.Ordinal))
                {
                    int x1 = line.IndexOf('[');
                    int x2 = line.IndexOf(']');
                    msg.PluralNum = int.Parse(line[(x1 + 1)..x2]);
                }

                if (msg.PluralNum >= 10)
                {
                    throw new InvalidOperationException("Too many plural forms");
                }

                msg.Val[msg.PluralNum] = Array.Empty<byte>();
                msg.Cur = MsgStr;
            }

            if (msg.Cur != MsgUnspec)
            {
                var tmp = ExtractString(line);
                if (tmp != null)
                {
                    switch (msg.Cur)
                    {
                        case MsgCtxt:
                            msg.Ctxt += tmp;
                            break;
                        case MsgId:
                            msg.Id += tmp;
                            break;
                        case MsgIdPlural:
                            msg.IdPlural += tmp;
                            break;
                        case MsgStr:
                            byte[] chunk = Encoding.UTF8.GetBytes(tmp);
                            var currentVal = msg.Val[msg.PluralNum] ?? Array.Empty<byte>();
                            byte[] combined = new byte[currentVal.Length + chunk.Length];
                            Buffer.BlockCopy(currentVal, 0, combined, 0, currentVal.Length);
                            Buffer.BlockCopy(chunk, 0, combined, currentVal.Length, chunk.Length);
                            msg.Val[msg.PluralNum] = combined;
                            break;
                    }
                }
            }
        }

        CommitMsg();

        // Binary packaging
        using var ms = new MemoryStream();
        uint currentOffset = 0;
        var indexedEntries = new List<LmoEntry>(entries.Count);

        foreach (var ent in entries)
        {
            var val = ent.Val;
            ms.Write(val, 0, val.Length);

            indexedEntries.Add(new LmoEntry
            {
                KeyId = ent.KeyId,
                Plural = ent.Plural,
                Offset = currentOffset,
                Length = (uint)val.Length,
                Val = val
            });

            currentOffset += (uint)val.Length;
            uint pad = currentOffset & 3;
            if (pad != 0)
            {
                int paddingBytes = (int)(4 - pad);
                for (int p = 0; p < paddingBytes; p++)
                {
                    ms.WriteByte(0);
                }
                currentOffset += (uint)paddingBytes;
            }
        }

        indexedEntries.Sort((a, b) => a.KeyId.CompareTo(b.KeyId));

        uint tableOffset = currentOffset;
        byte[] beBuffer = new byte[4];

        foreach (var ent in indexedEntries)
        {
            BinaryPrimitives.WriteUInt32BigEndian(beBuffer, ent.KeyId);
            ms.Write(beBuffer);

            BinaryPrimitives.WriteUInt32BigEndian(beBuffer, unchecked((uint)(ent.Plural + 1)));
            ms.Write(beBuffer);

            BinaryPrimitives.WriteUInt32BigEndian(beBuffer, ent.Offset);
            ms.Write(beBuffer);

            BinaryPrimitives.WriteUInt32BigEndian(beBuffer, ent.Length);
            ms.Write(beBuffer);
        }

        if (indexedEntries.Count > 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(beBuffer, tableOffset);
            ms.Write(beBuffer);
        }

        return ms.ToArray();
    }
}
