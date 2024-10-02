using Microsoft.Extensions.Logging;
using Serilog.Events;

public class SerilogLoggerAdapter<T> : ILogger<T>
{
    private readonly Serilog.ILogger _logger;

    public SerilogLoggerAdapter(Serilog.ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // Serilog does not natively support BeginScope, but you can return a no-op disposable.
        return new NoopDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(ConvertLogLevel(logLevel));
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var logMessage = formatter(state, exception);
        _logger.Write(ConvertLogLevel(logLevel), exception, logMessage);
    }

    private static LogEventLevel ConvertLogLevel(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => LogEventLevel.Verbose,
            LogLevel.Debug => LogEventLevel.Debug,
            LogLevel.Information => LogEventLevel.Information,
            LogLevel.Warning => LogEventLevel.Warning,
            LogLevel.Error => LogEventLevel.Error,
            LogLevel.Critical => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }

    private class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
