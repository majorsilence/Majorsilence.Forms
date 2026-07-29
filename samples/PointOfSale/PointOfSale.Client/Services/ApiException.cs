using System.Net;

namespace PointOfSale.Client.Services;

public sealed class ApiException(HttpStatusCode statusCode, string body)
    : Exception($"API request failed ({(int)statusCode} {statusCode}): {body}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}
