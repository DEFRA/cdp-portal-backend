namespace Defra.Cdp.Backend.Api.Utils;

using Microsoft.AspNetCore.Http;
using System.Text.Json;

public static class JsonExceptionMiddlewareExtensions
{
    public static void UseJsonExceptionHandler(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (BadHttpRequestException badEx) when (badEx.InnerException is JsonException jsonEx)
            {
                await WriteBadRequestJson(context, jsonEx.Message);
            }
            catch (JsonException jsonEx)
            {
                await WriteBadRequestJson(context, jsonEx.Message);
            }
        });
    }

    private static async Task WriteBadRequestJson(HttpContext context, string message)
    {
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";

        var payload = new { error = "Invalid request body", detail = message };

        await context.Response.WriteAsJsonAsync(payload);
    }
}