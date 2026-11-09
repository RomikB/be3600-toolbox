using System.Collections.ObjectModel;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Desktop.Logging;

public class UiLogService
{
    public ObservableCollection<UiLogMessage> Messages { get; } = new();

    public void Log(LogLevel level, string message, string? category = null)
    {
        var item = new UiLogMessage(DateTime.Now, level, message, category);
        if (Dispatcher.UIThread.CheckAccess())
        {
            AddMessage(item);
        }
        else
        {
            Dispatcher.UIThread.Post(() => AddMessage(item));
        }
    }

    private void AddMessage(UiLogMessage item)
    {
        if (Messages.Count > 1000)
        {
            Messages.RemoveAt(0);
        }
        Messages.Add(item);
    }

    public void Clear()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Messages.Clear();
        }
        else
        {
            Dispatcher.UIThread.Post(() => Messages.Clear());
        }
    }

    public string GetAllText()
    {
        return string.Join(Environment.NewLine, Messages.Select(m => m.FormattedText));
    }
}

