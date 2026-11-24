using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Data;
using MiMascota.Web.Models;
using MiMascota.Web.Options;

namespace MiMascota.Tests;

/// <summary>SQLite in-memory database that lives as long as the open connection.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>A clock frozen at a given local date and time.</summary>
public sealed class FixedTimeProvider(DateTime localNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(localNow, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

public static class TestData
{
    // Reference dates used across the tests (2026-11-24 is a Tuesday).
    public static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0);
    public static readonly DateOnly Today = new(2026, 11, 24);
    public static readonly DateOnly Tomorrow = new(2026, 11, 25);   // Wednesday
    public static readonly DateOnly Sunday = new(2026, 11, 29);

    public static Microsoft.Extensions.Options.IOptions<ClinicOptions> Clinic(
        TimeOnly? opening = null, TimeOnly? closing = null, int slotMinutes = 30) =>
        Microsoft.Extensions.Options.Options.Create(new ClinicOptions
        {
            OpeningTime = opening ?? new TimeOnly(9, 0),
            ClosingTime = closing ?? new TimeOnly(18, 0),
            SlotMinutes = slotMinutes,
            ClosedDays = [DayOfWeek.Sunday]
        });

    public static VetService AddService(AppDbContext db, int durationMinutes, string name = "Consulta")
    {
        var service = new VetService { Name = name, DurationMinutes = durationMinutes, Price = 300m };
        db.VetServices.Add(service);
        db.SaveChanges();
        return service;
    }

    public static Pet AddPet(AppDbContext db, string name = "Firulais")
    {
        var pet = new Pet
        {
            Name = name,
            Species = "Perro",
            Owner = new Owner { Name = "Dueño de prueba", Phone = "5512345678" }
        };
        db.Pets.Add(pet);
        db.SaveChanges();
        return pet;
    }

    public static Appointment AddAppointment(AppDbContext db, Pet pet, VetService service, DateTime start,
        AppointmentStatus status = AppointmentStatus.Pending)
    {
        var appointment = new Appointment
        {
            PetId = pet.Id,
            VetServiceId = service.Id,
            Start = start,
            End = start.AddMinutes(service.DurationMinutes),
            Status = status
        };
        db.Appointments.Add(appointment);
        db.SaveChanges();
        return appointment;
    }

    public static DateTime At(this DateOnly date, int hour, int minute = 0) =>
        date.ToDateTime(new TimeOnly(hour, minute));
}
