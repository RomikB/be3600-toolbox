using System.Reflection;
using System.Text;

namespace BE3600Toolbox.Engine.Assets;

public static class EmbeddedAssetReader
{
    private static readonly Assembly EngineAssembly = typeof(EmbeddedAssetReader).Assembly;

    public static Stream OpenRead(string assetFileName)
    {
        var resourceName = $"BE3600Toolbox.Engine.Assets.{assetFileName}";
        return EngineAssembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded resource '{resourceName}' not found. Available: {string.Join(", ", EngineAssembly.GetManifestResourceNames())}");
    }

    public static string ReadAsString(string assetFileName)
    {
        using var stream = OpenRead(assetFileName);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static byte[] ReadAsBytes(string assetFileName)
    {
        using var stream = OpenRead(assetFileName);
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}

