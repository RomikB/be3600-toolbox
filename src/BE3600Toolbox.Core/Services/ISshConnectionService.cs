using BE3600Toolbox.Core.Models;

namespace BE3600Toolbox.Core.Services;

public interface ISshConnectionService
{
    Task<bool> EnsureConnectedAsync(string targetIp, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
    Task<bool> CheckPortAsync(string targetIp, int port = 22, CancellationToken ct = default);
    Task<string?> GetCalculatedDefaultPasswordAsync(string targetIp, CancellationToken ct = default);
}

