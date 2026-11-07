using BE3600Toolbox.Core.Models;

namespace BE3600Toolbox.Core.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task UpdateAsync(Action<AppSettings> updateAction, CancellationToken ct = default);
}

