using System.Diagnostics.CodeAnalysis;
using Serilog.Core;
using Serilog.Events;

namespace Defra.Cdp.Backend.Api.Utils.Logging;

public static class LogFilters
{
    [ExcludeFromCodeCoverage]
    public class ExcludeHealthEndpoint : ILogEventFilter {
        public bool IsEnabled(LogEvent logEvent)
        {
            logEvent.Properties.TryGetValue("RequestPath", out var path);
            return path == null || path.ToString().Equals("/health");
        }
    }

}