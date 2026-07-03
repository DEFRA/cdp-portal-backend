using System.Diagnostics.CodeAnalysis;
using Defra.Cdp.Backend.Api.Utils.Auditing;
using Elastic.Serilog.Enrichers.Web;
using Serilog;
using Serilog.Events;

namespace Defra.Cdp.Backend.Api.Utils.Logging;

public static class CdpLogging
{
    [ExcludeFromCodeCoverage]
    public static void Configuration(HostBuilderContext ctx, LoggerConfiguration config)
    {
        var httpAccessor = ctx.Configuration.Get<HttpContextAccessor>();
        var traceIdHeader = ctx.Configuration.GetValue<string>("TraceHeader");

        var mainLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(ctx.Configuration)
            .Enrich.WithEcsHttpContext(httpAccessor!)
            .Enrich.FromLogContext()
            .Filter.With<AuditLogger.Filters.ExcludeAuditEvents>()
            .CreateLogger();

        if (traceIdHeader != null)
        {
            config.Enrich.WithCorrelationId(traceIdHeader);
        }

        var auditLogger = AuditLogger.CreateAuditLogger();

        // Must be set on `config`, not just mainLogger, or overrides are ignored.
        ApplyMinimumLevel(config, ctx.Configuration.GetSection("Serilog:MinimumLevel"));

        config
            .WriteTo.Logger(mainLogger)
            .WriteTo.Logger(auditLogger);
    }

    private static void ApplyMinimumLevel(LoggerConfiguration config, IConfigurationSection section)
    {
        var defaultLevel = section.GetValue<LogEventLevel?>("Default") ?? LogEventLevel.Information;
        config.MinimumLevel.Is(defaultLevel);

        foreach (var overrideEntry in section.GetSection("Override").GetChildren())
        {
            if (Enum.TryParse<LogEventLevel>(overrideEntry.Value, ignoreCase: true, out var level))
            {
                config.MinimumLevel.Override(overrideEntry.Key, level);
            }
        }
    }
}