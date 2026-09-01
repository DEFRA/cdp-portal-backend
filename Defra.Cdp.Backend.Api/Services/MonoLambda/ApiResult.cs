using System.Net;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda;

public sealed record ApiResult<T>(bool IsSuccess, HttpStatusCode StatusCode, string? ErrorMessage, T? Response)
{
    public static ApiResult<T> Success(T response) => new(true, HttpStatusCode.OK, null, response);

    public static ApiResult<T> Failure(HttpStatusCode statusCode, string message) =>
        new(false, statusCode, message, default);
}
