using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using BE3600Toolbox.Desktop.Logging;
using BE3600Toolbox.Desktop.ViewModels;
using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Desktop.Views;

public partial class MainWindow : Window
{
    private static readonly IBrush BrushTime = SolidColorBrush.Parse("#7F849C");
    private static readonly IBrush BrushCategory = SolidColorBrush.Parse("#9399B2");
    private static readonly IBrush BrushMessage = SolidColorBrush.Parse("#CDD6F4");
    private static readonly IBrush BrushInfo = SolidColorBrush.Parse("#89B4FA");
    private static readonly IBrush BrushWarn = SolidColorBrush.Parse("#F9E2AF");
    private static readonly IBrush BrushError = SolidColorBrush.Parse("#F38BA8");
    private static readonly IBrush BrushDebug = SolidColorBrush.Parse("#6C7086");

    private bool _isAtBottom = true;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (LogsScrollViewer != null)
            {
                LogsScrollViewer.ScrollChanged += OnLogsScrollChanged;
            }

            if (DataContext is MainWindowViewModel vm)
            {
                vm.Logs.CollectionChanged += OnLogsCollectionChanged;
                PopulateAllLogs(vm.Logs);
                await vm.InitializeAsync();
            }
        };
    }

    private void PopulateAllLogs(IEnumerable<UiLogMessage> logs)
    {
        if (LogsSelectableTextBlock == null) return;
        LogsSelectableTextBlock.Inlines?.Clear();
        foreach (var log in logs)
        {
            AppendLogMessage(log);
        }
    }

    private void AppendLogMessage(UiLogMessage item)
    {
        if (LogsSelectableTextBlock == null) return;
        if (LogsSelectableTextBlock.Inlines == null)
        {
            LogsSelectableTextBlock.Inlines = new InlineCollection();
        }

        var inlines = LogsSelectableTextBlock.Inlines;

        // 1. Time (gray)
        inlines.Add(new Run($"{item.FormattedTime} ") { Foreground = BrushTime });

        // 2. Level (colored)
        inlines.Add(new Run($"{item.LevelShort} ")
        {
            Foreground = GetLevelBrush(item.Level),
            FontWeight = FontWeight.Bold
        });

        // 3. Category (e.g. [MiWiFiClient])
        if (!string.IsNullOrEmpty(item.Category))
        {
            inlines.Add(new Run($"[{item.Category}] ") { Foreground = BrushCategory });
        }

        // 4. Message content
        inlines.Add(new Run(item.Message) { Foreground = BrushMessage });

        // 5. Line break
        inlines.Add(new LineBreak());
    }

    private static IBrush GetLevelBrush(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => BrushDebug,
        LogLevel.Information => BrushInfo,
        LogLevel.Warning => BrushWarn,
        LogLevel.Error or LogLevel.Critical => BrushError,
        _ => BrushMessage
    };

    private void OnLogsScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (LogsScrollViewer == null) return;

        var maxOffset = LogsScrollViewer.Extent.Height - LogsScrollViewer.Viewport.Height;
        if (maxOffset <= 0)
        {
            _isAtBottom = true;
        }
        else if (Math.Abs(e.OffsetDelta.Y) > 0.001)
        {
            // If user scrolled, check if they are near the bottom (within 25px tolerance)
            _isAtBottom = LogsScrollViewer.Offset.Y >= (maxOffset - 25);
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (UiLogMessage item in e.NewItems)
            {
                AppendLogMessage(item);
            }

            if (_isAtBottom && LogsScrollViewer != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    LogsScrollViewer.ScrollToEnd();
                }, DispatcherPriority.Background);
            }
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            LogsSelectableTextBlock?.Inlines?.Clear();
            _isAtBottom = true;
        }
    }
}
