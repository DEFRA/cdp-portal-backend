using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Defra.Cdp.Backend.Api.Utils.Audit;

public static class AuditLoggingExtension
{
    public const string AuditPropertyName = "IsAudit";
    
    // Serilog logger
    public static void Audit(this Serilog.ILogger logger,
        string message,
        params object?[] propertyValues)
    {
        ArgumentNullException.ThrowIfNull(logger);

        logger.ForContext(AuditPropertyName, true)
            .Information(message, propertyValues);
    }

    // Serilog logger
    public static void Audit(this Serilog.ILogger logger,
        Exception exception,
        string messageTemplate,
        params object?[] propertyValues)
    {
        ArgumentNullException.ThrowIfNull(logger);

        logger.ForContext(AuditPropertyName, true)
            .Information(exception, messageTemplate, propertyValues);
    }
    
    
    private static readonly Dictionary<string, object> s_auditLogLevel = new()
    {
        [AuditPropertyName] = true, ["log.level"] = "AUDIT"
    };
    
    // Microsoft Logger
    public static void Audit(this Microsoft.Extensions.Logging.ILogger logger,
        string message,
        params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);

        // When Serilog is the provider, this scope becomes a log-event property.
        using (logger.BeginScope(s_auditLogLevel))
        {
            logger.LogInformation(message, args);
        }
    }

    // Microsoft Logger
    public static void Audit(this Microsoft.Extensions.Logging.ILogger logger,
        Exception exception,
        string message,
        params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        using (logger.BeginScope(s_auditLogLevel))
        {
            logger.LogInformation(exception, message, args);
        }
    }

    public static ILoggingBuilder AddCdpAuditLogger(this ILoggingBuilder builder)
    {
        var auditLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.With<EnrichAuditLog>()
            .WriteTo.Console(new CompactJsonFormatter())
            .Filter.With<OnlyAuditEvents>()
            .CreateLogger();
        builder.AddSerilog(auditLogger);
        return builder;
    }
}

public class EnrichAuditLog : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("log.level", "AUDIT"));
    }
}

public class OnlyAuditEvents : ILogEventFilter {
    public bool IsEnabled(LogEvent logEvent)
    {
        return logEvent.Properties.ContainsKey(AuditLoggingExtension.AuditPropertyName);
    }
}

public class ExcludeAuditEvents : ILogEventFilter {
    public bool IsEnabled(LogEvent logEvent)
    {
        return !logEvent.Properties.ContainsKey(AuditLoggingExtension.AuditPropertyName);
    }
}