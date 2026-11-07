using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BE3600Toolbox.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRouterService _routerService;
    private readonly IFlashService _flashService;
    private readonly IInteractionService _interactionService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private string _routerIp = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isBusy;

    public MainWindowViewModel(
        IRouterService routerService,
        IFlashService flashService,
        IInteractionService interactionService,
        ISettingsService settingsService)
    {
        _routerService = routerService;
        _flashService = flashService;
        _interactionService = interactionService;
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading settings...";
        try
        {
            await _settingsService.LoadAsync();
            RouterIp = _settingsService.Settings.RouterIp;

            if (string.IsNullOrWhiteSpace(RouterIp))
            {
                await AutoDetectRouterAsync();
            }
            else
            {
                StatusMessage = $"Using saved router IP: {RouterIp}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Initialization error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task AutoDetectRouterAsync()
    {
        IsBusy = true;
        StatusMessage = "Auto-detecting router (192.168.31.1)...";
        try
        {
            var detected = await _routerService.DetectRouterAsync();
            if (detected != null)
            {
                RouterIp = detected.IpAddress;
                StatusMessage = $"Detected: {detected.Description} ({detected.IpAddress})";
                await _settingsService.UpdateAsync(s => s.RouterIp = RouterIp);
            }
            else
            {
                StatusMessage = "No stock router detected on 192.168.31.1. Please enter IP manually.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Detection error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckRouterAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Please specify a router IP address.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Connecting to router...";
        try
        {
            var info = await _routerService.GetDeviceInfoAsync(RouterIp);
            StatusMessage = $"Found {info.Model} (FW: {info.FirmwareVersion})";

            if (RouterIp != _settingsService.Settings.RouterIp)
            {
                await _settingsService.UpdateAsync(s => s.RouterIp = RouterIp);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UnlockSshAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Please specify a router IP address.";
            return;
        }

        var password = await _interactionService.PromptPasswordAsync("Enter router admin password:");
        if (string.IsNullOrEmpty(password))
        {
            StatusMessage = "Authentication cancelled.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Authenticating with router to acquire stok...";

        try
        {
            var stok = await _routerService.AcquireStokAsync(RouterIp, password);
            StatusMessage = $"Authentication successful! Acquired stok: {stok[..Math.Min(12, stok.Length)]}...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Authentication error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
