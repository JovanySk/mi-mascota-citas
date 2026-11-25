using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MiMascota.Web.Security;

namespace MiMascota.Web.ViewModels;

/// <summary>
/// Where the "Agenda una cita" / "Reservar" buttons go: staff open the booking form,
/// visitors go to the contact page with the request (and the service) already written.
/// </summary>
public static class BookingLinks
{
    public static string For(IUrlHelper url, ClaimsPrincipal user, int? serviceId = null, string? serviceName = null)
    {
        if (user.IsStaff())
        {
            return url.Action("Create", "Appointments", new { serviceId })!;
        }

        return serviceName is null
            ? url.Action("Contact", "Clinic", new { motivo = "cita" })!
            : url.Action("Contact", "Clinic", new { servicio = serviceName })!;
    }
}
