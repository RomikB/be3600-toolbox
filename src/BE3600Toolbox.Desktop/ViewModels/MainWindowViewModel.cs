using System.Collections.ObjectModel;
using BE3600Toolbox.Core.Models;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Core.Utilities;
using BE3600Toolbox.Desktop.Logging;
using BE3600Toolbox.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IRouterService _routerService;
    private readonly ISshConnectionService _sshConnectionService;
    private readonly ISshManager _sshManager;
    private readonly IFlashService _flashService;
    private readonly IInteractionService _interactionService;
    private readonly ISettingsService _settingsService;
    private readonly IClipboardService _clipboardService;
    private readonly UiLogService _logService;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private string _routerIp = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Готов к работе";

    [ObservableProperty]
    private string _currentStepName = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isProgressVisible;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _selectedTabIndex;

    // Device Information
    [ObservableProperty]
    private bool _isDeviceConnected;

    [ObservableProperty]
    private string _deviceModel = "—";

    [ObservableProperty]
    private string _deviceHardware = "—";

    [ObservableProperty]
    private string _deviceFirmware = "—";

    [ObservableProperty]
    private string _deviceChannel = "—";

    [ObservableProperty]
    private string _deviceSerialNumber = "—";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskedSshPassword))]
    private string _calculatedSshPassword = "—";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskedSshPassword))]
    [NotifyPropertyChangedFor(nameof(PasswordToggleIcon))]
    [NotifyPropertyChangedFor(nameof(PasswordToggleToolTip))]
    private bool _isPasswordVisible;

    public string MaskedSshPassword => IsPasswordVisible ? CalculatedSshPassword : (CalculatedSshPassword == "—" ? "—" : "••••••••");
    public string PasswordToggleIcon => IsPasswordVisible ? "🙈" : "👁";
    public string PasswordToggleToolTip => IsPasswordVisible ? "Скрыть пароль" : "Показать пароль";

    [ObservableProperty]
    private bool _isModelBe3600;

    // Status Badges
    [ObservableProperty]
    private string _httpBadgeText = "Ожидание";

    [ObservableProperty]
    private string _httpBadgeColor = "#888888";

    [ObservableProperty]
    private string _sshBadgeText = "Ожидание";

    [ObservableProperty]
    private string _sshBadgeColor = "#888888";

    [ObservableProperty]
    private string _modelBadgeText = "Не определена";

    [ObservableProperty]
    private string _modelBadgeColor = "#888888";

    // Logging & Console
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogPanelToggleText))]
    private bool _isLogExpanded;

    public string LogPanelToggleText => IsLogExpanded ? "▼ Скрыть журнал" : "▲ Показать журнал";

    public ObservableCollection<UiLogMessage> Logs => _logService.Messages;

    public MainWindowViewModel(
        IRouterService routerService,
        ISshConnectionService sshConnectionService,
        ISshManager sshManager,
        IFlashService flashService,
        IInteractionService interactionService,
        ISettingsService settingsService,
        IClipboardService clipboardService,
        UiLogService logService,
        ILogger<MainWindowViewModel> logger)
    {
        _routerService = routerService;
        _sshConnectionService = sshConnectionService;
        _sshManager = sshManager;
        _flashService = flashService;
        _interactionService = interactionService;
        _settingsService = settingsService;
        _clipboardService = clipboardService;
        _logService = logService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = "Загрузка настроек...";
        try
        {
            await _settingsService.LoadAsync();
            RouterIp = _settingsService.Settings.RouterIp;

            _logger.LogInformation("Приложение запущено. Язык: {Lang}, SSH-порт: {Port}",
                _settingsService.Settings.Language, _settingsService.Settings.SshPort);

            if (string.IsNullOrWhiteSpace(RouterIp))
            {
                await AutoDetectRouterAsync();
            }
            else
            {
                StatusMessage = $"Используется сохраненный IP: {RouterIp}";
                await RefreshDeviceInfoAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка инициализации настроек");
            StatusMessage = $"Ошибка инициализации: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private IProgress<StepProgress> CreateProgress()
    {
        return new Progress<StepProgress>(p =>
        {
            CurrentStepName = p.StepName;
            if (p.Percentage.HasValue)
            {
                ProgressValue = p.Percentage.Value;
                IsProgressVisible = true;
            }
            else
            {
                IsProgressVisible = true;
            }

            _logger.LogInformation("-> {Step} {Pct}", p.StepName, p.Percentage.HasValue ? $"[{p.Percentage:F0}%]" : "");
        });
    }

    [RelayCommand]
    public async Task AutoDetectRouterAsync()
    {
        var savedIp = _settingsService.Settings.RouterIp;
        var fallbackIp = !string.IsNullOrWhiteSpace(savedIp) ? savedIp : RouterIp;

        IsBusy = true;
        IsProgressVisible = true;
        ProgressValue = 20;
        StatusMessage = "Поиск роутера на 192.168.31.1...";
        CurrentStepName = "Отправка запроса xqsystem/init_info...";

        try
        {
            _logger.LogInformation("Запуск автоопределения роутера (192.168.31.1)...");
            var detected = await _routerService.DetectRouterAsync();
            if (detected != null)
            {
                RouterIp = detected.IpAddress;
                StatusMessage = $"Обнаружен: {detected.Description} ({detected.IpAddress})";
                _logger.LogInformation("Обнаружен роутер: {Desc} ({Ip})", detected.Description, detected.IpAddress);
                await _settingsService.UpdateAsync(s => s.RouterIp = RouterIp);
                await RefreshDeviceInfoAsync();
            }
            else
            {
                _logger.LogWarning("Роутер не ответил на стандартном адресе 192.168.31.1");
                if (!string.IsNullOrWhiteSpace(fallbackIp))
                {
                    RouterIp = fallbackIp;
                    _logger.LogInformation("Возврат к сохранённому IP ({Ip}) и обновление статуса...", fallbackIp);
                    await RefreshDeviceInfoAsync();
                    if (IsDeviceConnected)
                    {
                        StatusMessage = $"Роутер на 192.168.31.1 не ответил. Восстановлен статус для {fallbackIp}.";
                    }
                    else
                    {
                        StatusMessage = $"Роутер на 192.168.31.1 не ответил. Сохранённый IP ({fallbackIp}) также недоступен.";
                    }
                }
                else
                {
                    StatusMessage = "Роутер на 192.168.31.1 не ответил. Укажите IP вручную.";
                    SetOfflineStatus();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при автопоиске роутера");
            if (!string.IsNullOrWhiteSpace(fallbackIp))
            {
                RouterIp = fallbackIp;
                await RefreshDeviceInfoAsync();
                if (IsDeviceConnected)
                {
                    StatusMessage = $"Ошибка автопоиска. Восстановлен статус для {fallbackIp}.";
                }
                else
                {
                    StatusMessage = $"Ошибка поиска: {ex.Message}";
                }
            }
            else
            {
                StatusMessage = $"Ошибка поиска: {ex.Message}";
                SetOfflineStatus();
            }
        }
        finally
        {
            ProgressValue = 100;
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshDeviceInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Укажите IP-адрес роутера.";
            return;
        }

        IsBusy = true;
        IsProgressVisible = true;
        ProgressValue = 30;
        StatusMessage = "Опрос параметров устройства...";
        CurrentStepName = $"Подключение к http://{RouterIp}...";

        try
        {
            _logger.LogInformation("Получение информации об устройстве с {Ip}...", RouterIp);
            var info = await _routerService.GetDeviceInfoAsync(RouterIp);

            DeviceModel = string.IsNullOrWhiteSpace(info.Model) ? "Unknown" : info.Model;
            DeviceHardware = string.IsNullOrWhiteSpace(info.HardwareVersion) ? info.Model : info.HardwareVersion;
            DeviceFirmware = string.IsNullOrWhiteSpace(info.FirmwareVersion) ? "Unknown" : info.FirmwareVersion;
            DeviceChannel = string.IsNullOrWhiteSpace(info.Channel) ? "stable" : info.Channel;
            DeviceSerialNumber = string.IsNullOrWhiteSpace(info.SerialNumber) ? "Недоступен" : info.SerialNumber;

            IsDeviceConnected = true;
            HttpBadgeText = "Онлайн";
            HttpBadgeColor = "#4CAF50";

            // Verify BE3600 (RD15) model guardrail
            IsModelBe3600 = DeviceModel.Contains("RD15", StringComparison.OrdinalIgnoreCase) ||
                            DeviceHardware.Contains("RD15", StringComparison.OrdinalIgnoreCase);

            if (IsModelBe3600)
            {
                ModelBadgeText = "BE3600 (RD15)";
                ModelBadgeColor = "#4CAF50";
            }
            else
            {
                ModelBadgeText = DeviceModel;
                ModelBadgeColor = "#FF9800";
                _logger.LogWarning("Внимание: Обнаружена модель '{Model}', отличная от RD15 (BE3600)!", DeviceModel);
            }

            // Calculate SSH password from serial number
            if (!string.IsNullOrWhiteSpace(info.SerialNumber) && info.SerialNumber != "Недоступен")
            {
                var preparedSn = XiaomiPasswordCalculator.NormalizeSerialNumber(info.SerialNumber);
                CalculatedSshPassword = XiaomiPasswordCalculator.Calculate(preparedSn);
                _logger.LogInformation("Вычислен заводской SSH-пароль: {Pwd}", CalculatedSshPassword);
            }
            else
            {
                CalculatedSshPassword = "—";
            }

            // Check SSH Port
            ProgressValue = 70;
            CurrentStepName = "Проверка доступности порта SSH (22)...";
            var sshPort = _settingsService.Settings.SshPort;
            var isPortOpen = await _sshConnectionService.CheckPortAsync(RouterIp, sshPort);

            if (_sshManager.IsConnected)
            {
                SshBadgeText = "Подключен (root)";
                SshBadgeColor = "#4CAF50";
            }
            else if (isPortOpen)
            {
                SshBadgeText = $"Порт {sshPort} открыт";
                SshBadgeColor = "#FF9800";
            }
            else
            {
                SshBadgeText = "Порт 22 закрыт";
                SshBadgeColor = "#F44336";
            }

            StatusMessage = $"Успешно: {DeviceModel} (FW: {DeviceFirmware})";

            if (RouterIp != _settingsService.Settings.RouterIp)
            {
                await _settingsService.UpdateAsync(s => s.RouterIp = RouterIp);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка соединения с роутером {Ip}", RouterIp);
            StatusMessage = $"Ошибка соединения: {ex.Message}";
            SetOfflineStatus();
        }
        finally
        {
            ProgressValue = 100;
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UnlockSshAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Укажите IP-адрес роутера.";
            return;
        }

        // Safety Guardrail: check model
        if (!IsModelBe3600 && IsDeviceConnected)
        {
            var confirm = await _interactionService.ConfirmAsync(
                "Внимание: Неизвестная модель",
                $"Обнаружен роутер '{DeviceModel}', отличный от BE3600 (RD15).\nВыполнение эксплойта на неподдерживаемой модели может быть небезопасно.\n\nПродолжить выполнение?");
            if (!confirm)
            {
                StatusMessage = "Операция отменена пользователем.";
                return;
            }
        }

        var password = await _interactionService.PromptPasswordAsync("Введите пароль администратора роутера:");
        if (string.IsNullOrWhiteSpace(password))
        {
            StatusMessage = "Разблокировка отменена: не введен пароль администратора.";
            return;
        }

        IsBusy = true;
        IsProgressVisible = true;
        ProgressValue = 10;
        StatusMessage = "Авторизация в веб-интерфейсе роутера...";
        var progress = CreateProgress();

        try
        {
            _logger.LogInformation("Попытка входа в веб-интерфейс http://{Ip} для получения stok...", RouterIp);
            var stok = await _routerService.AcquireStokAsync(RouterIp, password);
            _logger.LogInformation("Авторизация успешна! Получен stok: {Stok}...", stok[..Math.Min(12, stok.Length)]);

            StatusMessage = "Отправка команд разблокировки SSH...";
            var success = await _routerService.UnlockSshAsync(RouterIp, stok, progress);
            if (success)
            {
                _logger.LogInformation("SSH успешно разблокирован на роутере!");
                StatusMessage = "SSH успешно разблокирован!";

                // Probe SSH port and update status
                await UpdateSshStatusBadgesAsync();

                // Try automatic SSH connection
                _logger.LogInformation("Проверка подключения к SSH...");
                await _sshConnectionService.EnsureConnectedAsync(RouterIp, progress);
                await UpdateSshStatusBadgesAsync();
            }
            else
            {
                _logger.LogWarning("Роутер отклонил одну или несколько команд разблокировки.");
                StatusMessage = "Ошибка: не удалось разблокировать SSH (роутер вернул ошибку).";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при разблокировке SSH");
            StatusMessage = $"Ошибка разблокировки SSH: {ex.Message}";
        }
        finally
        {
            ProgressValue = 100;
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task InstallLanguagesAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Укажите IP-адрес роутера.";
            return;
        }

        IsBusy = true;
        IsProgressVisible = true;
        StatusMessage = "Подготовка к установке локализации...";
        var progress = CreateProgress();

        try
        {
            _logger.LogInformation("Проверка SSH соединения перед установкой локализации...");
            var connected = await _sshConnectionService.EnsureConnectedAsync(RouterIp, progress);
            if (!connected)
            {
                StatusMessage = "Не удалось подключиться к роутеру по SSH. Локализация отменена.";
                return;
            }

            await UpdateSshStatusBadgesAsync();

            StatusMessage = "Установка русской и английской локализации...";
            var success = await _routerService.InstallLanguagesAsync(RouterIp, progress);
            if (success)
            {
                _logger.LogInformation("Локализация EN/RU успешно установлена.");
                StatusMessage = "Локализация EN/RU успешно установлена! Обновите страницу роутера (Ctrl+F5).";
            }
            else
            {
                _logger.LogWarning("Установка локализации завершилась с ошибкой.");
                StatusMessage = "Не удалось завершить установку локализации.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при установке языковых пакетов");
            StatusMessage = $"Ошибка установки локализации: {ex.Message}";
        }
        finally
        {
            ProgressValue = 100;
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UninstallLanguagesAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Укажите IP-адрес роутера.";
            return;
        }

        var confirm = await _interactionService.ConfirmAsync(
            "Удаление локализации",
            "Вы действительно хотите удалить русский и английский языковые пакеты и вернуть заводской китайский интерфейс?");
        if (!confirm)
        {
            StatusMessage = "Удаление локализации отменено пользователем.";
            return;
        }

        IsBusy = true;
        IsProgressVisible = true;
        StatusMessage = "Подключение к SSH для удаления локализации...";
        var progress = CreateProgress();

        try
        {
            var connected = await _sshConnectionService.EnsureConnectedAsync(RouterIp, progress);
            if (!connected)
            {
                StatusMessage = "Не удалось подключиться к роутеру по SSH. Удаление отменено.";
                return;
            }

            await UpdateSshStatusBadgesAsync();

            StatusMessage = "Удаление языковых пакетов...";
            var success = await _routerService.UninstallLanguagesAsync(RouterIp, progress);
            if (success)
            {
                _logger.LogInformation("Локализация EN/RU успешно удалена.");
                StatusMessage = "Локализация удалена. Восстановлен заводской китайский интерфейс.";
            }
            else
            {
                _logger.LogWarning("Удаление локализации завершилось с ошибкой.");
                StatusMessage = "Не удалось завершить удаление локализации.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении локализации");
            StatusMessage = $"Ошибка удаления локализации: {ex.Message}";
        }
        finally
        {
            ProgressValue = 100;
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task InstallPermanentSshAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Укажите IP-адрес роутера.";
            return;
        }

        IsBusy = true;
        IsProgressVisible = true;
        StatusMessage = "Подготовка к установке постоянного SSH...";
        var progress = CreateProgress();

        try
        {
            _logger.LogInformation("Проверка SSH соединения перед установкой постоянного SSH...");
            var connected = await _sshConnectionService.EnsureConnectedAsync(RouterIp, progress);
            if (!connected)
            {
                StatusMessage = "Не удалось подключиться к роутеру по SSH. Установка отменена.";
                return;
            }

            await UpdateSshStatusBadgesAsync();

            StatusMessage = "Установка постоянного SSH в автозапуск...";
            var success = await _routerService.InstallPermanentSshAsync(RouterIp, progress);
            if (success)
            {
                _logger.LogInformation("Постоянный SSH успешно установлен в автозапуск.");
                StatusMessage = "Постоянный SSH успешно установлен! Доступ сохранится после перезагрузки.";
            }
            else
            {
                _logger.LogWarning("Установка постоянного SSH завершилась с ошибкой.");
                StatusMessage = "Не удалось установить постоянный SSH.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при установке постоянного SSH");
            StatusMessage = $"Ошибка установки постоянного SSH: {ex.Message}";
        }
        finally
        {
            ProgressValue = 100;
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task UninstallPermanentSshAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Укажите IP-адрес роутера.";
            return;
        }

        var confirm = await _interactionService.ConfirmAsync(
            "Удаление постоянного SSH",
            "Вы действительно хотите удалить автозапуск службы dropbear и вернуть заводское поведение системы?");
        if (!confirm)
        {
            StatusMessage = "Удаление автозапуска SSH отменено пользователем.";
            return;
        }

        IsBusy = true;
        IsProgressVisible = true;
        StatusMessage = "Подключение к SSH для удаления автозапуска...";
        var progress = CreateProgress();

        try
        {
            var connected = await _sshConnectionService.EnsureConnectedAsync(RouterIp, progress);
            if (!connected)
            {
                StatusMessage = "Не удалось подключиться к роутеру по SSH. Удаление отменено.";
                return;
            }

            await UpdateSshStatusBadgesAsync();

            StatusMessage = "Удаление постоянного SSH из автозапуска...";
            var success = await _routerService.UninstallPermanentSshAsync(RouterIp, progress);
            if (success)
            {
                _logger.LogInformation("Постоянный SSH успешно удален из автозапуска.");
                StatusMessage = "Автозапуск постоянного SSH успешно удалён.";
            }
            else
            {
                _logger.LogWarning("Удаление постоянного SSH завершилось с ошибкой.");
                StatusMessage = "Не удалось удалить автозапуск постоянного SSH.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении постоянного SSH");
            StatusMessage = $"Ошибка удаления постоянного SSH: {ex.Message}";
        }
        finally
        {
            ProgressValue = 100;
            IsProgressVisible = false;
            IsBusy = false;
        }
    }

    private async Task UpdateSshStatusBadgesAsync()
    {
        var port = _settingsService.Settings.SshPort;
        var isOpen = await _sshConnectionService.CheckPortAsync(RouterIp, port);
        if (_sshManager.IsConnected)
        {
            SshBadgeText = "Подключен (root)";
            SshBadgeColor = "#4CAF50";
        }
        else if (isOpen)
        {
            SshBadgeText = $"Порт {port} открыт";
            SshBadgeColor = "#FF9800";
        }
        else
        {
            SshBadgeText = "Порт 22 закрыт";
            SshBadgeColor = "#F44336";
        }
    }

    [RelayCommand]
    public async Task TestSshConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(RouterIp))
        {
            StatusMessage = "Укажите IP-адрес роутера.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Проверка SSH-подключения...";
        var progress = CreateProgress();

        try
        {
            _logger.LogInformation("Запуск проверки SSH-подключения к {Ip}...", RouterIp);
            var success = await _sshConnectionService.EnsureConnectedAsync(RouterIp, progress);
            if (success)
            {
                StatusMessage = "SSH подключен успешно!";
                _logger.LogInformation("SSH сессия активна.");
            }
            else
            {
                StatusMessage = "Не удалось подключиться по SSH.";
            }
            await UpdateSshStatusBadgesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка проверки SSH");
            StatusMessage = $"Ошибка SSH: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsProgressVisible = false;
        }
    }

    [RelayCommand]
    public async Task CopySshPasswordAsync()
    {
        if (!string.IsNullOrWhiteSpace(CalculatedSshPassword) && CalculatedSshPassword != "—")
        {
            await _clipboardService.SetTextAsync(CalculatedSshPassword);
            StatusMessage = "Пароль SSH скопирован в буфер обмена.";
            _logger.LogInformation("Пароль SSH скопирован в буфер обмена.");
        }
    }

    [RelayCommand]
    public void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    public void ClearLogs()
    {
        _logService.Clear();
    }

    [RelayCommand]
    public async Task CopyLogsAsync()
    {
        var text = _logService.GetAllText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _clipboardService.SetTextAsync(text);
            StatusMessage = "Журнал логов скопирован в буфер обмена.";
        }
    }

    [RelayCommand]
    public void ToggleLogsPanel()
    {
        IsLogExpanded = !IsLogExpanded;
    }

    private void SetOfflineStatus()
    {
        IsDeviceConnected = false;
        HttpBadgeText = "Офлайн";
        HttpBadgeColor = "#F44336";
        SshBadgeText = "Недоступен";
        SshBadgeColor = "#888888";
        ModelBadgeText = "Не определена";
        ModelBadgeColor = "#888888";
    }
}
