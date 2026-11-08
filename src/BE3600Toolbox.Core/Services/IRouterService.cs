using BE3600Toolbox.Core.Models;

namespace BE3600Toolbox.Core.Services;

public interface IRouterService
{
    Task<DetectedRouter?> DetectRouterAsync(CancellationToken ct = default);
    Task<RouterInfo> GetDeviceInfoAsync(string ipAddress, CancellationToken ct = default);
    Task<string> AcquireStokAsync(string ipAddress, string password, CancellationToken ct = default);
    Task<bool> UnlockSshAsync(string ipAddress, string stok, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
    Task<bool> InstallLanguagesAsync(string ipAddress, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
    Task<bool> UninstallLanguagesAsync(string ipAddress, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
    Task<bool> InstallPermanentSshAsync(string ipAddress, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
    Task<bool> UninstallPermanentSshAsync(string ipAddress, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
}
