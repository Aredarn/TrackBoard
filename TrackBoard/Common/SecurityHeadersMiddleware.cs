namespace TrackBoard.Common;

/// <summary>
/// Adds the response headers a JSON API should always send. Spring Security sets these by
/// default; ASP.NET Core does not, so they are applied explicitly here.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Stop browsers from MIME-sniffing a JSON response into something executable.
        headers["X-Content-Type-Options"] = "nosniff";

        // This API returns no HTML, so framing it is never legitimate.
        headers["X-Frame-Options"] = "DENY";

        headers["Referrer-Policy"] = "no-referrer";

        // A locked-down CSP suits JSON responses, but it would also block the Scalar docs UI
        // from loading its own scripts, so it is scoped to the API surface only.
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        }

        return next(context);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityHeadersMiddleware>();
}
