using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Libplanet.TestUtilities.Logging;

internal sealed class TestOutputLoggerProvider(ITestOutputHelper output, LogLevel minLevel = LogLevel.Debug)
    : ILoggerProvider
{
    private readonly ITestOutputHelper _output = output;
    private readonly LogLevel _minLevel = minLevel;
    private readonly ConcurrentDictionary<string, ILogger> _loggers = new();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new TestOutputLogger(name, _output, _minLevel));

    public void Dispose() => _loggers.Clear();

    private sealed class TestOutputLogger(string category, ITestOutputHelper output, LogLevel minLevel) : ILogger
    {
        private readonly string _category = category;
        private readonly ITestOutputHelper _output = output;
        private readonly LogLevel _minLevel = minLevel;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            try
            {
                var message = formatter(state, exception);
                _output.WriteLine($"{DateTime.UtcNow:O} {logLevel,-5} {eventId.Id,4} {_category}: {message}");
                if (exception is not null)
                {
                    _output.WriteLine(exception.ToString());
                }
            }
            catch (InvalidOperationException)
            {
                // nothing
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
