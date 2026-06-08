namespace SpendingApi.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME type sniffing — browser must use declared Content-Type
        headers["X-Content-Type-Options"] = "nosniff";

        // Block this response from being embedded in an iframe — clickjacking protection
        headers["X-Frame-Options"] = "DENY";

        // Restrict what resources can be loaded — this is a pure JSON API, nothing else needed
        headers["Content-Security-Policy"] = "default-src 'none'";

        // Don't send the referring URL when navigating away — no URL leakage
        headers["Referrer-Policy"] = "no-referrer";

        // Enforce HTTPS for 1 year — browser won't make plain HTTP requests to this origin
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Opt out of browser features we don't use
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await _next(context);
    }
}
