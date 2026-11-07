using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace BE3600Toolbox.Cli.Logging;

public sealed class CleanConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "clean";

    public CleanConsoleFormatter() : base(FormatterName)
    {
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message) && logEntry.Exception == null)
        {
            return;
        }

        var levelBadge = logEntry.LogLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "none"
        };

        var levelColor = logEntry.LogLevel switch
        {
            LogLevel.Trace or LogLevel.Debug => ConsoleColor.DarkGray,
            LogLevel.Information => ConsoleColor.DarkCyan,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error or LogLevel.Critical => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };

        var prevColor = Console.ForegroundColor;

        // Timestamp
        Console.ForegroundColor = ConsoleColor.DarkGray;
        textWriter.Write($"[{DateTime.Now:HH:mm:ss}] ");

        // Log level badge
        Console.ForegroundColor = levelColor;
        textWriter.Write($"[{levelBadge}] ");

        // Message
        Console.ForegroundColor = ConsoleColor.Gray;
        textWriter.WriteLine(message);

        // Exception details if present
        if (logEntry.Exception != null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            textWriter.WriteLine(logEntry.Exception.ToString());
        }

        Console.ForegroundColor = prevColor;
    }
}

