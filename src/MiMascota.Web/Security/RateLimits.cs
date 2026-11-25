using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace MiMascota.Web.Security;

/// <summary>
/// Per-IP limits for the two forms anyone can post: the staff login and the public contact form.
/// Defaults: 5 posts per minute each ("RateLimits:LoginPerMinute", "RateLimits:ContactPerMinute").
/// </summary>
public static class RateLimits
{
    public const string Login = "login";
    public const string Contact = "contact";

    public static IServiceCollection AddFormRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // The limits are read per request (not at startup) so tests can change them.
            options.AddPolicy(Login, context => PerIp(context, Login, Limit(context, "RateLimits:LoginPerMinute")));
            options.AddPolicy(Contact, context => PerIp(context, Contact, Limit(context, "RateLimits:ContactPerMinute")));
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var response = rejected.HttpContext.Response;
                response.Headers.RetryAfter = "60";
                response.ContentType = "text/html; charset=utf-8";
                await response.WriteAsync(RejectedPage, cancellationToken);
            };
        });

        return services;
    }

    private static int Limit(HttpContext context, string key) =>
        context.RequestServices.GetRequiredService<IConfiguration>().GetValue(key, 5);

    private static RateLimitPartition<string> PerIp(HttpContext context, string policy, int permitsPerMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{policy}:{context.Connection.RemoteIpAddress ?? IPAddress.Loopback}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

    /// <summary>Small standalone page (the limiter answers before MVC runs).</summary>
    private const string RejectedPage = """
        <!DOCTYPE html>
        <html lang="es-MX">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <title>Demasiados intentos · Mi Mascota</title>
            <link rel="icon" href="/img/paw.svg" type="image/svg+xml" />
            <link rel="stylesheet" href="/css/site.css" />
        </head>
        <body class="is-inner">
            <main class="mm-container page-main">
                <section class="mm-card error-card">
                    <img src="/img/mascotas/frenchie-cono.webp" alt="Bulldog francés con collar isabelino" width="536" height="828" />
                    <div>
                        <span class="eyebrow">Un momento</span>
                        <h1 class="page-title">Demasiados intentos</h1>
                        <p class="page-sub">Recibimos demasiados envíos desde tu conexión. Espera un minuto y vuelve a intentarlo.</p>
                        <a href="/" class="mm-btn mm-btn-primary mm-btn-lg">Volver al inicio</a>
                    </div>
                </section>
            </main>
        </body>
        </html>
        """;
}
