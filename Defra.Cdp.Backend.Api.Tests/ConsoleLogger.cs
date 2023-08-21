using Microsoft.Extensions.Logging;

namespace Defra.Cdp.Backend.Api.Tests;

public static class ConsoleLogger
{
    private static readonly ILoggerFactory _loggerFactory;

    static ConsoleLogger()
    {
        var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        _loggerFactory = LoggerFactory.Create(c => c.AddConsole());
    }

    public static ILogger CreateLogger(string name)
    {
        return _loggerFactory.CreateLogger(name);
    }

    public static ILogger<T> CreateLogger<T>()
    {
        return _loggerFactory.CreateLogger<T>();
    }
}