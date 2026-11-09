using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Desktop.Logging;

public record UiLogMessage(DateTime Timestamp, LogLevel Level, string Message, string? Category = null)
{
    public string FormattedTime => Timestamp.ToString("HH:mm:ss");

    public string LevelShort => Level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "LOG"
    };

    public string LevelColor => Level switch
    {
        LogLevel.Trace or LogLevel.Debug => "#6C7086",
        LogLevel.Information => "#89B4FA",
        LogLevel.Warning => "#F9E2AF",
        LogLevel.Error or LogLevel.Critical => "#F38BA8",
        _ => "#CDD6F4"
    };

    public string FormattedText => $"[{FormattedTime} {LevelShort}] {Message}";
}

