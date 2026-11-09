using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Engine.Ssh;

public class SshConnectionService : ISshConnectionService
{
    private readonly ISshManager _sshManager;
    private readonly IRouterService _routerService;
    private readonly ISettingsService _settingsService;
    private readonly IInteractionService _interactionService;
    private readonly ILogger<SshConnectionService> _logger;

    public SshConnectionService(
        ISshManager sshManager,
        IRouterService routerService,
        ISettingsService settingsService,
        IInteractionService interactionService,
        ILogger<SshConnectionService> logger)
    {
        _sshManager = sshManager;
        _routerService = routerService;
        _settingsService = settingsService;
        _interactionService = interactionService;
        _logger = logger;
    }

    public async Task<bool> CheckPortAsync(string targetIp, int port = 22, CancellationToken ct = default)
    {
        _logger.LogInformation("Checking SSH availability on {TargetIp}:{Port}...", targetIp, port);
        var isOpen = await _sshManager.IsPortOpenAsync(targetIp, port, ct: ct).ConfigureAwait(false);
        if (!isOpen)
        {
            _logger.LogWarning("SSH port {Port} is closed or unreachable on {TargetIp}", port, targetIp);
            return false;
        }

        _logger.LogInformation("SSH port {Port} is open on {TargetIp}", port, targetIp);
        return true;
    }

    public async Task<string?> GetCalculatedDefaultPasswordAsync(string targetIp, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("Retrieving router serial number via init_info from {TargetIp}...", targetIp);
            var info = await _routerService.GetDeviceInfoAsync(targetIp, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(info.SerialNumber))
            {
                var preparedSn = XiaomiPasswordCalculator.NormalizeSerialNumber(info.SerialNumber);
                var pwd = XiaomiPasswordCalculator.Calculate(preparedSn);
                _logger.LogInformation("Router S/N detected: {Sn}, calculated default password: {Pwd}", preparedSn, pwd);
                return pwd;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve device serial number from {TargetIp}", targetIp);
        }

        return null;
    }

    public async Task<bool> EnsureConnectedAsync(
        string targetIp,
        IProgress<StepProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (_sshManager.IsConnected)
        {
            return true;
        }

        var port = _settingsService.Settings.SshPort;
        var user = _settingsService.Settings.SshUser;

        progress?.Report(new StepProgress($"Checking SSH port {port} on {targetIp}...", 10));
        var isPortOpen = await CheckPortAsync(targetIp, port, ct).ConfigureAwait(false);
        if (!isPortOpen)
        {
            progress?.Report(new StepProgress($"SSH port {port} is closed. Unlock SSH first.", 0));
            return false;
        }

        // 1. Try calculated password from serial number
        progress?.Report(new StepProgress("Calculating default SSH password from router S/N...", 30));
        var defaultPassword = await GetCalculatedDefaultPasswordAsync(targetIp, ct).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(defaultPassword))
        {
            progress?.Report(new StepProgress($"Authenticating with default password...", 50));
            _logger.LogInformation("Attempting SSH connection to {TargetIp}:{Port} with calculated password...", targetIp, port);

            if (await _sshManager.TryConnectAsync(targetIp, port, user, defaultPassword, ct).ConfigureAwait(false))
            {
                _logger.LogInformation("SSH connected successfully using calculated default password!");
                progress?.Report(new StepProgress("SSH connected successfully", 100));
                return true;
            }

            _logger.LogWarning("Calculated default password was rejected by router");
        }

        // 2. Prompt user for password if default failed or was unavailable
        while (!ct.IsCancellationRequested)
        {
            progress?.Report(new StepProgress("Awaiting SSH password from user...", 60));
            var password = await _interactionService.PromptPasswordAsync($"Enter SSH password for '{user}@{targetIp}':", ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogInformation("SSH authentication cancelled by user");
                progress?.Report(new StepProgress("SSH authentication cancelled", 0));
                return false;
            }

            progress?.Report(new StepProgress("Authenticating with entered password...", 80));
            _logger.LogInformation("Authenticating with custom password...");
            if (await _sshManager.TryConnectAsync(targetIp, port, user, password, ct).ConfigureAwait(false))
            {
                _logger.LogInformation("SSH connected successfully!");
                progress?.Report(new StepProgress("SSH connected successfully", 100));
                return true;
            }

            _logger.LogWarning("SSH authentication failed with provided password");
        }

        return false;
    }
}

