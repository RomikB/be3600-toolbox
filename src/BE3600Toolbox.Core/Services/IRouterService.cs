using BE3600Toolbox.Core.Models;

namespace BE3600Toolbox.Core.Services;

public interface IRouterService
{
    Task<DetectedRouter?> DetectRouterAsync(CancellationToken ct = default);
    Task<RouterInfo> GetDeviceInfoAsync(string ipAddress, CancellationToken ct = default);
    Task<string> AcquireStokAsync(string ipAddress, string password, CancellationToken ct = default);
    Task<bool> UnlockSshAsync(string ipAddress, string stok, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
    Task<bool> InstallLocalizationAsync(string ipAddress, string language, IProgress<StepProgress>? progress = null, CancellationToken ct = default);
}
