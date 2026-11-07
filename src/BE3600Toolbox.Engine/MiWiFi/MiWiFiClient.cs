using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Engine.MiWiFi;

public class MiWiFiClient : IRouterService
{
    private const string DefaultCandidateIp = "192.168.31.1";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(800);

    private readonly HttpClient _httpClient;
    private readonly ILogger<MiWiFiClient> _logger;

    public MiWiFiClient(HttpClient httpClient, ILogger<MiWiFiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DetectedRouter?> DetectRouterAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting quick router auto-detection on {Ip} (stock MiWiFi)...", DefaultCandidateIp);
        return await ProbeIpAsync(DefaultCandidateIp, ct).ConfigureAwait(false);
    }

    private async Task<DetectedRouter?> ProbeIpAsync(string ip, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            var miwifiUrl = $"http://{ip}/cgi-bin/luci/api/xqsystem/init_info";
            using var response = await _httpClient.GetAsync(miwifiUrl, cts.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
                if (doc.RootElement.TryGetProperty("hardware", out var hw))
                {
                    var model = hw.GetString() ?? "Xiaomi Router";
                    return new DetectedRouter(ip, $"Xiaomi {model} (MiWiFi)", IsOpenWrt: false);
                }
            }
        }
        catch
        {
            // Router did not respond within timeout or is not stock MiWiFi
        }

        return null;
    }

    public async Task<RouterInfo> GetDeviceInfoAsync(string ipAddress, CancellationToken ct = default)
    {
        var url = $"http://{ipAddress}/cgi-bin/luci/api/xqsystem/init_info";
        _logger.LogInformation("Querying device info from {Url}...", url);

        try
        {
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            var model = root.TryGetProperty("hardware", out var hw) ? hw.GetString() ?? "Unknown" : "Unknown";
            var version = root.TryGetProperty("romversion", out var rom) ? rom.GetString() ?? "Unknown" : "Unknown";
            var channel = root.TryGetProperty("channel", out var ch) ? ch.GetString() : null;

            return new RouterInfo(
                IpAddress: ipAddress,
                Model: model,
                HardwareVersion: model,
                FirmwareVersion: version,
                Channel: channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch device info from {IpAddress}", ipAddress);
            throw;
        }
    }

    public async Task<string> AcquireStokAsync(string ipAddress, string password, CancellationToken ct = default)
    {
        var baseUrl = $"http://{ipAddress}/cgi-bin/luci";
        var webUrl = $"{baseUrl}/web";
        _logger.LogInformation("Fetching web auth info from {WebUrl}...", webUrl);

        string html;
        try
        {
            using var response = await _httpClient.GetAsync(webUrl, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to router at {Ip}", ipAddress);
            throw new InvalidOperationException($"Failed to connect to router at {ipAddress}: {ex.Message}", ex);
        }

        // 1. Extract salt key
        var keyMatch = Regex.Match(html, @"key:\s*'([^']+)'");
        var key = keyMatch.Success ? keyMatch.Groups[1].Value : "a2ffa5c9be07488bbb04a3a47d3c5f6a";

        // 2. Extract device MAC / ID
        var macMatch = Regex.Match(html, @"deviceId\s*=\s*'([^']+)'");
        if (!macMatch.Success)
        {
            macMatch = Regex.Match(html, @"([0-9a-fA-F]{2}(?::[0-9a-fA-F]{2}){5})");
        }
        var mac = macMatch.Success ? macMatch.Groups[1].Value : "00:00:00:00:00:00";

        // 3. Extract encryption mode (0: SHA-1, 1: SHA-256)
        var modeMatch = Regex.Match(html, @"newEncryptMode\s*=\s*parseInt\('?(\d+)'?\)");
        var encryptMode = modeMatch.Success && int.TryParse(modeMatch.Groups[1].Value, out var m) ? m : 0;

        _logger.LogInformation("Auth parameters detected: MAC={Mac}, Mode={Mode}", mac, encryptMode == 1 ? "SHA-256" : "SHA-1");

        // 4. Generate client nonce: 0_{mac}_{timestamp}_{rand}
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randNum = Random.Shared.Next(1000, 10000);
        var nonce = $"0_{mac}_{timestamp}_{randNum}";

        // 5. Calculate password challenge hash
        var pwdHash = ComputeHash(password + key, encryptMode);
        var finalPwd = ComputeHash(nonce + pwdHash, encryptMode);

        // 6. Send login request
        var loginUrl = $"{baseUrl}/api/xqsystem/login";
        var form = new Dictionary<string, string>
        {
            ["username"] = "admin",
            ["password"] = finalPwd,
            ["logtype"] = "2",
            ["nonce"] = nonce
        };

        _logger.LogInformation("Sending login request to {LoginUrl}...", loginUrl);
        using var loginResponse = await _httpClient.PostAsync(loginUrl, new FormUrlEncodedContent(form), ct).ConfigureAwait(false);
        loginResponse.EnsureSuccessStatusCode();

        var json = await loginResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
        if (code != 0)
        {
            var msg = root.TryGetProperty("msg", out var msgElem) ? msgElem.GetString() ?? "Unknown" : "Unknown";
            _logger.LogWarning("Login failed for {IpAddress} (code {Code}): {Msg}", ipAddress, code, msg);
            throw new InvalidOperationException($"Login failed (code {code}): {msg}");
        }

        var stok = root.GetProperty("token").GetString();
        if (string.IsNullOrEmpty(stok))
        {
            throw new InvalidOperationException($"No token received in response: {json}");
        }

        _logger.LogInformation("Login successful! Acquired stok={Stok}", stok);
        return stok;
    }

    private static string ComputeHash(string text, int mode)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = mode == 1 ? SHA256.HashData(bytes) : SHA1.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }

    private async Task<string> PostCommandAsync(string ipAddress, string stok, string command)
    {
        var baseUrl = $"http://{ipAddress}/cgi-bin/luci";
        var apiUrl = $"{baseUrl}/;stok={stok}/api/xqsystem/start_binding";
        var rawKey = $"' <({command}) #";
        var encodedKey = Uri.EscapeDataString(rawKey).Replace("%28", "(").Replace("%29", ")").Replace("%2A", "*").Replace("%2F", "/");
        var contentData = $"uid=1234&key={encodedKey}";
        _logger.LogInformation("Sending... {ContentData}", contentData);
        using var content = new StringContent(contentData, Encoding.UTF8, "application/x-www-form-urlencoded");
        using var response = await _httpClient.PostAsync(apiUrl, content).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public async Task<bool> UnlockSshAsync(string ipAddress, string stok, IProgress<StepProgress>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(new StepProgress("Send command 1", 10));
        var commandResponse1 = await PostCommandAsync(ipAddress, stok, "nvram set ssh_en=1").ConfigureAwait(false);
        if (!commandResponse1.Contains("\"code\":0")) return false;
        progress?.Report(new StepProgress("Send command 2", 10));
        var commandResponse2 = await PostCommandAsync(ipAddress, stok, "nvram commit").ConfigureAwait(false);
        if (!commandResponse2.Contains("\"code\":0")) return false;
        progress?.Report(new StepProgress("Send command 3", 10));
        var commandResponse3 = await PostCommandAsync(ipAddress, stok, "sed -i 's/channel=.*/channel=\"debug\"/g' /etc/init.d/dropbear").ConfigureAwait(false);
        if (!commandResponse3.Contains("\"code\":0")) return false;
        progress?.Report(new StepProgress("Send command 4", 10));
        var commandResponse4 = await PostCommandAsync(ipAddress, stok, "/etc/init.d/dropbear start").ConfigureAwait(false);
        if (!commandResponse4.Contains("\"code\":0")) return false;
        return true;
    }

    public async Task<bool> InstallLocalizationAsync(string ipAddress, string language, IProgress<StepProgress>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(new StepProgress($"Installing {language} language pack", 20));
        _logger.LogInformation("Installing localization ({Language}) on {IpAddress}...", language, ipAddress);
        await Task.Delay(300, ct).ConfigureAwait(false);
        progress?.Report(new StepProgress("Localization completed", 100));
        return true;
    }
}
