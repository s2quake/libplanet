using Microsoft.Extensions.Logging;

namespace Libplanet.TestUtilities.Logging;

public static class TestLogging
{
    public static ILogger<T> CreateLogger<T>(ITestOutputHelper output, LogLevel minLevel = LogLevel.Debug)
    {
        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minLevel);
            builder.AddProvider(new TestOutputLoggerProvider(output, minLevel));
        });
        return factory.CreateLogger<T>();
    }
}
