using System;
using System.Net;

namespace doc_bursa.Infrastructure.ExternalApis.Monobank
{
    public class MonobankApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public MonobankApiException(string message, HttpStatusCode statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public class MonobankRateLimitException : MonobankApiException
    {
        public MonobankRateLimitException(string message)
            : base(message, HttpStatusCode.TooManyRequests)
        {
        }
    }

    public class MonobankInvalidTokenException : MonobankApiException
    {
        public MonobankInvalidTokenException(string message)
            : base(message, HttpStatusCode.Unauthorized)
        {
        }
    }
}
