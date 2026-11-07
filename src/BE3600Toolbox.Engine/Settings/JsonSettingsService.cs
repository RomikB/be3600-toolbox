using System.Text.Json;
using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Engine.Settings;

public class JsonSettingsService : ISettingsService
{
    private readonly ILogger<JsonSettingsService> _logger;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AppSettings Settings { get; private set; } = new();

    public JsonSettingsService(ILogger<JsonSettingsService> logger)
    {
        _logger = logger;
        _filePath = ResolveSettingsPath();
    }

    private static string ResolveSettingsPath()
    {
        var portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        if (File.Exists(portablePath))
        {
            return portablePath;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "BE3600Toolbox");
        return Path.Combine(configDir, "settings.json");
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Settings file not found at {Path}. Using default settings.", _filePath);
                Settings = new AppSettings();
                return;
            }

            await using var stream = File.OpenRead(_filePath);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions, ct).ConfigureAwait(false);
            Settings = loaded ?? new AppSettings();
            _logger.LogInformation("Loaded settings from {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings from {Path}. Using defaults.", _filePath);
            Settings = new AppSettings();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tempPath = $"{_filePath}.tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, Settings, _jsonOptions, ct).ConfigureAwait(false);
            }

            File.Move(tempPath, _filePath, overwrite: true);
            _logger.LogInformation("Saved settings to {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _filePath);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(Action<AppSettings> updateAction, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(updateAction);
        updateAction(Settings);
        await SaveAsync(ct).ConfigureAwait(false);
    }
}
