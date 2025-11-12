namespace Defra.Cdp.Backend.Api.Endpoints;

public class Helpers
{
    private static ILogger? _logger;

    public static List<string>? ExtractGroups(HttpContext httpContext, ILoggerFactory loggerFactory)
    {
        _logger ??= loggerFactory.CreateLogger<Helpers>();
        if (httpContext.User.Identity is not { IsAuthenticated: true })
        {
            _logger.LogError("User is not authenticated");
            return null;
        }

        var groups = httpContext.User.Claims.Where(c => c.Type == "groups").Select(c => c.Value).ToList();
        if (groups.Count == 0) _logger.LogError("User is not part of a valid group");
        return groups;
    }
}