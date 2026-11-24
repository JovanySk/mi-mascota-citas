using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Models;
using MiMascota.Web.Options;

namespace MiMascota.Web.Data;

/// <summary>
/// Fictional demo data. Appointments are created relative to "today" so the
/// dashboard and the availability screen always have something to show.
/// </summary>
public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db, ClinicOptions clinic, TimeProvider time)
    {
        if (await db.Owners.AnyAsync())
        {
            return; // Already seeded.
        }

        var maria = new Owner { Name = "María Fernanda López", Phone = "55 1234 5678", Email = "maria.lopez@example.com" };
        var carlos = new Owner { Name = "Carlos Ramírez Soto", Phone = "55 2345 6789", Email = "carlos.ramirez@example.com" };
        var ana = new Owner { Name = "Ana Sofía Hernández", Phone = "33 3456 7890", Email = "ana.hernandez@example.com" };
        var luis = new Owner { Name = "Luis Alberto Castillo", Phone = "81 4567 8901" };

        var firulais = new Pet { Name = "Firulais", Species = "Perro", Breed = "Labrador", Owner = maria };
        var michi = new Pet { Name = "Michi", Species = "Gato", Breed = "Siamés", Owner = maria };
        var rocky = new Pet { Name = "Rocky", Species = "Perro", Breed = "Bulldog francés", Owner = carlos };
        var luna = new Pet { Name = "Luna", Species = "Gato", Breed = "Mestizo", Owner = ana };
        var kiwi = new Pet { Name = "Kiwi", Species = "Ave", Breed = "Periquito australiano", Owner = luis };
        var toby = new Pet { Name = "Toby", Species = "Perro", Breed = "Chihuahua", Owner = luis };

        var consulta = new VetService { Name = "Consulta general", DurationMinutes = 30, Price = 350m };
        var vacuna = new VetService { Name = "Vacunación", DurationMinutes = 30, Price = 280m };
        var estetica = new VetService { Name = "Baño y estética", DurationMinutes = 60, Price = 420m };
        var cirugia = new VetService { Name = "Esterilización", DurationMinutes = 90, Price = 2500m };

        // First open day = today (if the clinic opens today); second = the next open day.
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var day1 = NextOpenDay(today, clinic);
        var day2 = NextOpenDay(day1.AddDays(1), clinic);

        db.AddRange(maria, carlos, ana, luis);
        db.AddRange(firulais, michi, rocky, luna, kiwi, toby);
        db.AddRange(consulta, vacuna, estetica, cirugia);
        db.AddRange(
            Create(firulais, consulta, day1, 9, 0, AppointmentStatus.Attended, "Revisión anual."),
            Create(michi, vacuna, day1, 11, 0, AppointmentStatus.Confirmed, "Vacuna triple felina."),
            Create(rocky, estetica, day1, 13, 0, AppointmentStatus.Confirmed, null),
            Create(luna, consulta, day1, 16, 30, AppointmentStatus.Pending, "No quiere comer desde ayer."),
            Create(kiwi, consulta, day2, 9, 30, AppointmentStatus.Pending, "Revisión de plumaje."),
            Create(toby, vacuna, day2, 10, 0, AppointmentStatus.Confirmed, "Refuerzo antirrábica."),
            Create(rocky, consulta, day2, 12, 0, AppointmentStatus.Cancelled, "El dueño canceló por teléfono."),
            Create(firulais, cirugia, day2, 15, 0, AppointmentStatus.Pending, "Ayuno de 12 horas."));

        await db.SaveChangesAsync();
    }

    private static Appointment Create(Pet pet, VetService service, DateOnly day, int hour, int minute,
        AppointmentStatus status, string? notes)
    {
        var start = day.ToDateTime(new TimeOnly(hour, minute));
        return new Appointment
        {
            Pet = pet,
            VetService = service,
            Start = start,
            End = start.AddMinutes(service.DurationMinutes),
            Status = status,
            Notes = notes
        };
    }

    private static DateOnly NextOpenDay(DateOnly from, ClinicOptions clinic)
    {
        var day = from;
        while (clinic.ClosedDays.Contains(day.DayOfWeek))
        {
            day = day.AddDays(1);
        }
        return day;
    }
}
