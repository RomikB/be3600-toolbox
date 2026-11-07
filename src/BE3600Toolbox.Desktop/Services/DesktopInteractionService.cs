using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using BE3600Toolbox.Core.Services;
using BE3600Toolbox.Desktop.Views;

namespace BE3600Toolbox.Desktop.Services;

public class DesktopInteractionService : IInteractionService
{
    public Task<bool> ConfirmAsync(string title, string message, bool defaultAnswer = false, CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }

    public async Task<string> PromptPasswordAsync(string message, CancellationToken ct = default)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
        {
            var dialog = new PasswordDialogWindow(message);
            var result = await dialog.ShowDialog<string?>(desktop.MainWindow);
            return result ?? string.Empty;
        }

        return string.Empty;
    }

    public Task<string> PromptInputAsync(string message, string defaultValue = "", CancellationToken ct = default)
    {
        return Task.FromResult(defaultValue);
    }

    public Task<T> SelectOptionAsync<T>(string title, IReadOnlyList<T> options, CancellationToken ct = default) where T : notnull
    {
        return Task.FromResult(options.First());
    }
}
