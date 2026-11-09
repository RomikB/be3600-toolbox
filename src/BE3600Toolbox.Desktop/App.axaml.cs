using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Desktop.Logging;
using BE3600Toolbox.Desktop.Services;
using BE3600Toolbox.Desktop.ViewModels;
using BE3600Toolbox.Desktop.Views;
using BE3600Toolbox.Engine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        var uiLogService = new UiLogService();
        services.AddSingleton(uiLogService);

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new UiLoggerProvider(uiLogService));
        });

        services.AddSingleton<IClipboardService, DesktopClipboardService>();
        services.AddSingleton<IInteractionService, DesktopInteractionService>();
        services.AddBe3600Engine();
        services.AddTransient<MainWindowViewModel>();
    }
}
