using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;

namespace MiMascota.Web.ViewComponents;

/// <summary>
/// A footer link for one service: staff open the booking form with it selected, visitors the
/// contact page with the request written (see BookingLinks).
/// </summary>
public record FooterServiceLink(string Label, int? ServiceId, string ServiceName);

/// <summary>
/// "Servicios" column of the footer. Each label is matched to a service by name when the
/// page renders; if the service was renamed or deleted, the link goes to the services list.
/// </summary>
public class FooterServicesViewComponent(AppDbContext db, ILogger<FooterServicesViewComponent> logger) : ViewComponent
{
    /// <summary>Footer label → service name in the catalog.</summary>
    public static readonly (string Label, string ServiceName)[] Links =
    [
        ("Consulta general", "Consulta general"),
        ("Vacunas", "Vacunación"),
        ("Baño y estética", "Baño y estética"),
        ("Exóticos", "Consulta exóticos")
    ];

    public async Task<IViewComponentResult> InvokeAsync()
    {
        Dictionary<string, int> ids;
        try
        {
            var services = await db.VetServices.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync();
            ids = services
                .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Min(s => s.Id), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            // The footer must never break a page (e.g. the error page when the database is down).
            logger.LogWarning(e, "Could not load the services for the footer.");
            ids = new Dictionary<string, int>();
        }

        var links = Links
            .Select(l => new FooterServiceLink(l.Label, ids.TryGetValue(l.ServiceName, out var id) ? id : null, l.ServiceName))
            .ToList();
        return View(links);
    }
}
