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

        // Owners (fictional names, phones and e-mails).
        var maria = new Owner { Name = "María Fernanda López", Phone = "55 1234 5678", Email = "maria.lopez@example.com" };
        var carlos = new Owner { Name = "Carlos Ramírez Soto", Phone = "55 2345 6789", Email = "carlos.ramirez@example.com" };
        var ana = new Owner { Name = "Ana Ruiz Hernández", Phone = "33 3456 7890", Email = "ana.ruiz@example.com" };
        var luis = new Owner { Name = "Luis Pérez Castillo", Phone = "81 4567 8901" };
        var sofia = new Owner { Name = "Sofía Díaz Morales", Phone = "55 5678 9012", Email = "sofia.diaz@example.com" };
        var valeria = new Owner { Name = "Valeria Cruz Ortega", Phone = "55 6789 0123", Email = "valeria.cruz@example.com" };
        var diego = new Owner { Name = "Diego Herrera Vázquez", Phone = "22 7890 1234" };
        var paola = new Owner { Name = "Paola Mendoza Reyes", Phone = "55 8901 2345", Email = "paola.mendoza@example.com" };

        // Pets: dogs of several breeds, cats, guinea pigs, hamsters, rabbits and some exotic patients.
        var toby = new Pet { Name = "Toby", Species = "Perro", Breed = "Jack Russell", Owner = ana };
        var rocky = new Pet { Name = "Rocky", Species = "Perro", Breed = "Bulldog francés", Owner = carlos };
        var firulais = new Pet { Name = "Firulais", Species = "Perro", Breed = "Golden retriever", Owner = maria };
        var kira = new Pet { Name = "Kira", Species = "Perro", Breed = "Husky siberiano", Owner = diego };
        var frijol = new Pet { Name = "Frijol", Species = "Perro", Breed = "Salchicha", Owner = paola };
        var chispa = new Pet { Name = "Chispa", Species = "Perro", Breed = "Chihuahua", Owner = luis };
        var michi = new Pet { Name = "Michi", Species = "Gato", Breed = "British shorthair", Owner = maria };
        var luna = new Pet { Name = "Luna", Species = "Gato", Breed = "Persa blanco", Owner = valeria };
        var canela = new Pet { Name = "Canela", Species = "Cuyo", Breed = "Peruano", Owner = sofia };
        var galleta = new Pet { Name = "Galleta", Species = "Cuyo", Breed = "Abisinio", Owner = diego };
        var nube = new Pet { Name = "Nube", Species = "Hámster", Breed = "Hámster sirio", Owner = valeria };
        var bolita = new Pet { Name = "Bolita", Species = "Hámster", Breed = "Hámster ruso", Owner = sofia };
        var copito = new Pet { Name = "Copito", Species = "Conejo", Breed = "Belier", Owner = luis };
        var pelusa = new Pet { Name = "Pelusa", Species = "Conejo", Breed = "Cabeza de león", Owner = paola };
        var paco = new Pet { Name = "Paco", Species = "Ave", Breed = "Cotorro cabeza amarilla", Owner = carlos };
        var kiwi = new Pet { Name = "Kiwi", Species = "Ave", Breed = "Perico australiano", Owner = sofia };
        var manuelita = new Pet { Name = "Manuelita", Species = "Tortuga", Breed = "Tortuga de orejas rojas", Owner = ana };
        var rex = new Pet { Name = "Rex", Species = "Iguana", Breed = "Iguana verde", Owner = diego };
        var bandido = new Pet { Name = "Bandido", Species = "Hurón", Breed = "Hurón canela", Owner = luis };

        var consulta = new VetService { Name = "Consulta general", DurationMinutes = 30, Price = 350m };
        var vacuna = new VetService { Name = "Vacunación", DurationMinutes = 30, Price = 280m };
        var estetica = new VetService { Name = "Baño y estética", DurationMinutes = 60, Price = 250m };
        var cirugia = new VetService { Name = "Esterilización", DurationMinutes = 90, Price = 2500m };
        var exoticos = new VetService { Name = "Consulta exóticos", DurationMinutes = 30, Price = 450m };
        var unas = new VetService { Name = "Corte de uñas", DurationMinutes = 30, Price = 150m };

        // First open day = today (if the clinic opens today); second = the next open day.
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var day1 = NextOpenDay(today, clinic);
        var day2 = NextOpenDay(day1.AddDays(1), clinic);

        db.AddRange(maria, carlos, ana, luis, sofia, valeria, diego, paola);
        db.AddRange(toby, rocky, firulais, kira, frijol, chispa, michi, luna, canela, galleta, nube, bolita, copito, pelusa,
            paco, kiwi, manuelita, rex, bandido);
        db.AddRange(consulta, vacuna, estetica, cirugia, exoticos, unas);
        db.AddRange(
            // Day 1: every status appears at least once.
            Create(toby, consulta, day1, 9, 0, AppointmentStatus.Attended, "Revisión anual."),
            Create(copito, exoticos, day1, 10, 0, AppointmentStatus.Attended, "Revisión de oreja."),
            Create(canela, unas, day1, 11, 30, AppointmentStatus.Confirmed, null),
            Create(rocky, consulta, day1, 12, 30, AppointmentStatus.Confirmed, "Control post cirugía."),
            Create(nube, exoticos, day1, 13, 30, AppointmentStatus.Pending, "Come menos semillas que de costumbre."),
            Create(paco, exoticos, day1, 14, 0, AppointmentStatus.Confirmed, "Se arranca las plumas del pecho."),
            Create(kira, vacuna, day1, 15, 0, AppointmentStatus.Pending, "Refuerzo de séxtuple."),
            Create(michi, vacuna, day1, 16, 0, AppointmentStatus.Cancelled, "La dueña canceló por teléfono."),
            Create(luna, estetica, day1, 17, 0, AppointmentStatus.Confirmed, "Desenredar el pelo del lomo."),
            // Day 2: a lighter agenda, so the quick-booking card shows free and taken slots.
            Create(firulais, estetica, day2, 9, 30, AppointmentStatus.Confirmed, null),
            Create(rex, exoticos, day2, 11, 0, AppointmentStatus.Pending, "Revisión de piel: no termina de mudar."),
            Create(chispa, vacuna, day2, 12, 0, AppointmentStatus.Pending, "Refuerzo antirrábica."),
            Create(pelusa, exoticos, day2, 13, 0, AppointmentStatus.Cancelled, "Se reagendará la próxima semana."),
            Create(galleta, unas, day2, 14, 30, AppointmentStatus.Confirmed, null),
            Create(manuelita, exoticos, day2, 15, 30, AppointmentStatus.Confirmed, "Caparazón blando en un costado."),
            Create(frijol, consulta, day2, 16, 0, AppointmentStatus.Pending, "Dolor de espalda al subir escaleras."),
            Create(bolita, exoticos, day2, 17, 0, AppointmentStatus.Pending, null));

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
