using Microsoft.Extensions.Logging;

namespace BE3600Toolbox.Desktop.Logging;

public class UiLoggerProvider : ILoggerProvider
{
    private readonly UiLogService _logService;

    public UiLoggerProvider(UiLogService logService)
    {
        _logService = logService;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new UiLogger(categoryName, _logService);
    }

    public void Dispose()
    {
    }

    private class UiLogger : ILogger
    {
        private readonly string _category;
        private readonly UiLogService _logService;

        public UiLogger(string category, UiLogService logService)
        {
            _category = category;
            _logService = logService;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var msg = formatter(state, exception);
            if (exception != null)
            {
                msg = $"{msg}{Environment.NewLine}{exception.Message}";
            }

            var shortCategory = _category.Contains('.') ? _category[(_category.LastIndexOf('.') + 1)..] : _category;
            _logService.Log(logLevel, msg, shortCategory);
        }
    }
}

