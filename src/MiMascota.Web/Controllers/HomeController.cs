using Microsoft.AspNetCore.Mvc;
using MiMascota.Web.Services;
using MiMascota.Web.ViewModels;

namespace MiMascota.Web.Controllers;

public class HomeController(IAppointmentService appointments, TimeProvider time) : Controller
{
    public async Task<IActionResult> Index()
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var model = new DashboardViewModel
        {
            Today = today,
            TodayAppointments = await appointments.ListAsync(today, status: null)
        };
        return View(model);
    }

    public IActionResult Error() => View();
}
