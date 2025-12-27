using System.Net;

namespace FinDesk.Services
{
    public record ApiResult<T>(bool Success, T Data, string? ErrorMessage = null, HttpStatusCode? StatusCode = null)
    {
        public static ApiResult<T> FromSuccess(T data) => new(true, data);

        public static ApiResult<T> FromError(string message, HttpStatusCode? status = null, T? data = default)
            => new(false, data!, message, status);
    }
}

