using System.Security.Cryptography;
using System.Text;

namespace BE3600Toolbox.Core.Utilities;

public static class XiaomiPasswordCalculator
{
    private const string DefaultSalt = "6d2df50a-250f-4a30-a5e6-d44fb0960aa0";

    /// <summary>
    /// Calculates the default SSH root password from the router's serial number (SN).
    /// Algorithm: MD5(SN + salt)[..8] (as used by miwifi.dev/ssh and official Xiaomi tools).
    /// </summary>
    /// <param name="serialNumber">The device serial number (e.g. 12345/A1B2C3D4 or 42385/XXXXXXXX).</param>
    /// <param name="salt">Optional custom salt, defaults to the standard Xiaomi router salt.</param>
    /// <returns>The 8-character root password.</returns>
    public static string Calculate(string serialNumber, string salt = DefaultSalt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);

        var payload = serialNumber.Trim() + salt;
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(payload));
        var hexString = Convert.ToHexStringLower(hashBytes);

        return hexString[..8];
    }
}

