using Microsoft.Extensions.Logging;

namespace AIUsageWidget.Infrastructure.Logging;

public sealed class RotatingFileLoggerProvider : ILoggerProvider
{
    private readonly LocalAppPaths _paths;
    private readonly long _maxBytes;
    private readonly object _gate = new();

    public RotatingFileLoggerProvider(LocalAppPaths paths, long maxBytes = 1_000_000)
    {
        _paths = paths;
        _maxBytes = maxBytes;
        _paths.EnsureCreated();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(_paths, categoryName, _maxBytes, _gate);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly LocalAppPaths _paths;
        private readonly string _categoryName;
        private readonly long _maxBytes;
        private readonly object _gate;

        public FileLogger(LocalAppPaths paths, string categoryName, long maxBytes, object gate)
        {
            _paths = paths;
            _categoryName = categoryName;
            _maxBytes = maxBytes;
            _gate = gate;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var file = Path.Combine(_paths.Logs, $"app-{DateTimeOffset.Now:yyyy-MM-dd}.log");
            var line = $"{DateTimeOffset.Now:O} [{logLevel}] {_categoryName}: {formatter(state, exception)}{Environment.NewLine}";
            if (exception is not null)
            {
                line += exception.GetType().Name + ": " + exception.Message + Environment.NewLine;
            }

            lock (_gate)
            {
                if (File.Exists(file) && new FileInfo(file).Length > _maxBytes)
                {
                    File.Move(file, file + ".1", overwrite: true);
                }

                File.AppendAllText(file, line);
            }
        }
    }
}
