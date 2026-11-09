using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace BE3600Toolbox.Desktop.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}

public class DesktopClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(text);
        }
    }
}

