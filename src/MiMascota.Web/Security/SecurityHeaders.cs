namespace MiMascota.Web.Security;

/// <summary>
/// Basic security headers on every response (pages, JSON, static files and photos).
/// The Content-Security-Policy allows only what the app really loads: its own files,
/// Bootstrap (jsDelivr), jQuery + validation (cdnjs) and the OpenStreetMap iframe on "Cómo llegar".
/// Inline scripts are not allowed; inline styles are, because a few views set CSS variables
/// with style="--i: 2" (animation delays).
/// </summary>
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "frame-src https://www.openstreetmap.org; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'; " +
        "base-uri 'self'; " +
        "object-src 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["X-Frame-Options"] = "DENY";
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            await next();
        });
}
