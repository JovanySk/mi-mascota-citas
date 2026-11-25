using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiMascota.Web.Data;
using MiMascota.Web.Options;
using MiMascota.Web.Services;
using MiMascota.Web.ViewModels;

namespace MiMascota.Web.Controllers;

public class HomeController(
    IAppointmentService appointments,
    AppDbContext db,
    IOptions<ClinicOptions> clinicOptions,
    TimeProvider time) : Controller
{
    public async Task<IActionResult> Index()
    {
        var clinic = clinicOptions.Value;
        var schedule = new ClinicSchedule(clinic);
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        var services = await db.VetServices.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        var bath = services.FirstOrDefault(s => s.Name.Contains("baño", StringComparison.OrdinalIgnoreCase));

        // The quick-booking card always offers the next open day after today.
        var quickDate = today.AddDays(1);
        for (var i = 0; i < 7 && schedule.IsClosed(quickDate); i++)
        {
            quickDate = quickDate.AddDays(1);
        }

        var model = new DashboardViewModel
        {
            Today = today,
            TodayAppointments = await appointments.ListAsync(today, status: null),
            HoursLabel = clinic.HoursLabel(),
            OpenDaysLabel = clinic.OpenDaysLabel(),
            SlotMinutes = clinic.SlotMinutes,
            OpeningTime = clinic.OpeningTime,
            ClosingTime = clinic.ClosingTime,
            BathService = bath,
            QuickService = bath ?? services.FirstOrDefault(),
            QuickDate = quickDate
        };
        return View(model);
    }

    public IActionResult Error() => View();
}
