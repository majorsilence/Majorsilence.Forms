using System.Security.Claims;

namespace PointOfSale.Api.Security;

public static class HttpContextExtensions
{
    public static int? GetCurrentUserId(this HttpContext http)
    {
        var claim = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
