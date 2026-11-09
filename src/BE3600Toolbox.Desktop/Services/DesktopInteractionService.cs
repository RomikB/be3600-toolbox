using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Desktop.Views;

namespace BE3600Toolbox.Desktop.Services;

public class DesktopInteractionService : IInteractionService
{
    public async Task<bool> ConfirmAsync(string title, string message, bool defaultAnswer = false, CancellationToken ct = default)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ConfirmDialogWindow(title, message);
                var result = await dialog.ShowDialog<bool?>(desktop.MainWindow);
                return result ?? defaultAnswer;
            });
        }

        return defaultAnswer;
    }

    public async Task<string> PromptPasswordAsync(string message, CancellationToken ct = default)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new PasswordDialogWindow(message);
                var result = await dialog.ShowDialog<string?>(desktop.MainWindow);
                return result ?? string.Empty;
            });
        }

        return string.Empty;
    }

    public async Task<string> PromptInputAsync(string message, string defaultValue = "", CancellationToken ct = default)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new InputDialogWindow(message, defaultValue);
                var result = await dialog.ShowDialog<string?>(desktop.MainWindow);
                return result ?? defaultValue;
            });
        }

        return defaultValue;
    }

    public Task<T> SelectOptionAsync<T>(string title, IReadOnlyList<T> options, CancellationToken ct = default) where T : notnull
    {
        return Task.FromResult(options.First());
    }
}
