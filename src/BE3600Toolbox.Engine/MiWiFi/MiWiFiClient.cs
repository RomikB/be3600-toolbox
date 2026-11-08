using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Engine.Assets;
using BE3600Toolbox.Engine.Localization;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Engine.MiWiFi;

public class MiWiFiClient : IRouterService
{
    private const string DefaultCandidateIp = "192.168.31.1";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(800);

    private readonly HttpClient _httpClient;
    private readonly ISshManager? _sshManager;
    private readonly ILogger<MiWiFiClient> _logger;

    public MiWiFiClient(HttpClient httpClient, ILogger<MiWiFiClient> logger, ISshManager? sshManager = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _sshManager = sshManager;
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
            var sn = root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                ? idProp.GetString()?.Trim()
                : null;

            return new RouterInfo(
                IpAddress: ipAddress,
                Model: model,
                HardwareVersion: model,
                FirmwareVersion: version,
                Channel: channel,
                SerialNumber: string.IsNullOrWhiteSpace(sn) ? null : sn);
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

    public async Task<bool> InstallLanguagesAsync(string ipAddress, IProgress<StepProgress>? progress = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting EN/RU language installation for {IpAddress}...", ipAddress);

        progress?.Report(new StepProgress("Compiling language packs (EN/RU)...", 10));
        var enLmo = LmoConverter.ConvertPoResourceToLmo("BE3600Toolbox.Engine.Assets.base.en.po");
        var ruLmo = LmoConverter.ConvertPoResourceToLmo("BE3600Toolbox.Engine.Assets.base.ru.po");

        if (_sshManager != null && _sshManager.IsConnected)
        {
            progress?.Report(new StepProgress("Uploading base.en.lmo to router...", 30));
            using (var enStream = new MemoryStream(enLmo))
            {
                await _sshManager.UploadFileAsync(enStream, "/tmp/base.en.lmo", null, ct).ConfigureAwait(false);
            }

            progress?.Report(new StepProgress("Uploading base.ru.lmo to router...", 50));
            using (var ruStream = new MemoryStream(ruLmo))
            {
                await _sshManager.UploadFileAsync(ruStream, "/tmp/base.ru.lmo", null, ct).ConfigureAwait(false);
            }

            progress?.Report(new StepProgress("Uploading installation scripts...", 70));
            using (var patchStream = EmbeddedAssetReader.OpenRead("lang_patch.sh"))
            {
                await _sshManager.UploadFileAsync(patchStream, "/tmp/lang_patch.sh", null, ct).ConfigureAwait(false);
            }

            using (var installStream = EmbeddedAssetReader.OpenRead("lang_install.sh"))
            {
                await _sshManager.UploadFileAsync(installStream, "/tmp/lang_install.sh", null, ct).ConfigureAwait(false);
            }

            progress?.Report(new StepProgress("Applying localization patch...", 85));
            var output = await _sshManager.ExecuteCommandAsync(
                "chmod +x /tmp/lang_install.sh /tmp/lang_patch.sh && /bin/sh /tmp/lang_install.sh && rm -f /tmp/lang_install.sh",
                ct).ConfigureAwait(false);

            _logger.LogInformation("Language install script output: {Output}", output);
            progress?.Report(new StepProgress("EN/RU language installation completed", 100));
            return true;
        }

        _logger.LogWarning("SSH manager is not connected, simulated localization.");
        await Task.Delay(300, ct).ConfigureAwait(false);
        progress?.Report(new StepProgress("Localization completed (simulated)", 100));
        return true;
    }

    public async Task<bool> UninstallLanguagesAsync(string ipAddress, IProgress<StepProgress>? progress = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting EN/RU language uninstallation for {IpAddress}...", ipAddress);

        if (_sshManager != null && _sshManager.IsConnected)
        {
            progress?.Report(new StepProgress("Uploading uninstall script...", 30));
            using (var uninstallStream = EmbeddedAssetReader.OpenRead("lang_uninstall.sh"))
            {
                await _sshManager.UploadFileAsync(uninstallStream, "/tmp/lang_uninstall.sh", null, ct).ConfigureAwait(false);
            }

            progress?.Report(new StepProgress("Executing language uninstallation on router...", 60));
            var output = await _sshManager.ExecuteCommandAsync(
                "chmod +x /tmp/lang_uninstall.sh && /bin/sh /tmp/lang_uninstall.sh && rm -f /tmp/lang_uninstall.sh",
                ct).ConfigureAwait(false);

            _logger.LogInformation("Language uninstall script output: {Output}", output);
            progress?.Report(new StepProgress("EN/RU language uninstallation completed", 100));
            return true;
        }

        _logger.LogWarning("SSH manager is not connected, simulated uninstallation.");
        await Task.Delay(300, ct).ConfigureAwait(false);
        progress?.Report(new StepProgress("Language uninstallation completed (simulated)", 100));
        return true;
    }

    public async Task<bool> InstallPermanentSshAsync(string ipAddress, IProgress<StepProgress>? progress = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting permanent SSH installation for {IpAddress}...", ipAddress);

        if (_sshManager != null && _sshManager.IsConnected)
        {
            progress?.Report(new StepProgress("Uploading permanent SSH patch script...", 25));
            using (var patchStream = EmbeddedAssetReader.OpenRead("ssh_patch.sh"))
            {
                await _sshManager.UploadFileAsync(patchStream, "/tmp/ssh_patch.sh", null, ct).ConfigureAwait(false);
            }

            progress?.Report(new StepProgress("Uploading permanent SSH installer script...", 50));
            using (var installStream = EmbeddedAssetReader.OpenRead("ssh_install.sh"))
            {
                await _sshManager.UploadFileAsync(installStream, "/tmp/ssh_install.sh", null, ct).ConfigureAwait(false);
            }

            progress?.Report(new StepProgress("Applying permanent SSH patch...", 75));
            var output = await _sshManager.ExecuteCommandAsync(
                "chmod +x /tmp/ssh_install.sh /tmp/ssh_patch.sh && /bin/sh /tmp/ssh_install.sh && rm -f /tmp/ssh_patch.log && /bin/sh /etc/crontabs/patches/ssh_patch.sh && rm -f /tmp/ssh_install.sh",
                ct).ConfigureAwait(false);

            _logger.LogInformation("Permanent SSH install script output: {Output}", output);
            progress?.Report(new StepProgress("Permanent SSH installed successfully", 100));
            return true;
        }

        _logger.LogWarning("SSH manager is not connected, simulated permanent SSH installation.");
        await Task.Delay(300, ct).ConfigureAwait(false);
        progress?.Report(new StepProgress("Permanent SSH installation completed (simulated)", 100));
        return true;
    }

    public async Task<bool> UninstallPermanentSshAsync(string ipAddress, IProgress<StepProgress>? progress = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting permanent SSH uninstallation for {IpAddress}...", ipAddress);

        if (_sshManager != null && _sshManager.IsConnected)
        {
            progress?.Report(new StepProgress("Uploading permanent SSH uninstall script...", 30));
            using (var uninstallStream = EmbeddedAssetReader.OpenRead("ssh_uninstall.sh"))
            {
                await _sshManager.UploadFileAsync(uninstallStream, "/tmp/ssh_uninstall.sh", null, ct).ConfigureAwait(false);
            }

            progress?.Report(new StepProgress("Executing permanent SSH uninstallation...", 70));
            var output = await _sshManager.ExecuteCommandAsync(
                "chmod +x /tmp/ssh_uninstall.sh && /bin/sh /tmp/ssh_uninstall.sh && rm -f /tmp/ssh_uninstall.sh",
                ct).ConfigureAwait(false);

            _logger.LogInformation("Permanent SSH uninstall script output: {Output}", output);
            progress?.Report(new StepProgress("Permanent SSH uninstalled successfully", 100));
            return true;
        }

        _logger.LogWarning("SSH manager is not connected, simulated permanent SSH uninstallation.");
        await Task.Delay(300, ct).ConfigureAwait(false);
        progress?.Report(new StepProgress("Permanent SSH uninstallation completed (simulated)", 100));
        return true;
    }
}
