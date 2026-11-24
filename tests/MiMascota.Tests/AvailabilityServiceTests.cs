using MiMascota.Web.Models;
using MiMascota.Web.Options;
using MiMascota.Web.Services;
using Microsoft.Extensions.Options;

namespace MiMascota.Tests;

public class AvailabilityServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private AvailabilityService CreateService(DateTime? now = null, IOptions<ClinicOptions>? options = null) =>
        new(_database.CreateContext(), options ?? TestData.Clinic(), new FixedTimeProvider(now ?? TestData.Now));

    private static List<string> Times(Result<IReadOnlyList<TimeSlot>> result) =>
        result.Value!.Select(s => s.Start.ToString("HH:mm")).ToList();

    [Fact]
    public async Task Thirty_minute_service_gets_every_slot_of_the_grid()
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);

        var result = await CreateService().GetAvailableSlotsAsync(TestData.Tomorrow, service.Id);

        Assert.True(result.IsSuccess);
        var times = Times(result);
        Assert.Equal(18, times.Count); // 09:00 … 17:30
        Assert.Equal("09:00", times.First());
        Assert.Equal("17:30", times.Last());
        Assert.Contains("12:30", times);
    }

    [Theory]
    [InlineData(60, 17, "17:00")]
    [InlineData(90, 16, "16:30")]
    public async Task Longer_services_must_finish_before_closing(int duration, int expectedCount, string expectedLast)
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, duration);

        var result = await CreateService().GetAvailableSlotsAsync(TestData.Tomorrow, service.Id);

        var times = Times(result);
        Assert.Equal(expectedCount, times.Count);
        Assert.Equal(expectedLast, times.Last());
        Assert.All(result.Value!, slot => Assert.True(slot.End <= TestData.Tomorrow.At(18)));
    }

    [Fact]
    public async Task Existing_appointment_blocks_every_overlapping_slot()
    {
        using var db = _database.CreateContext();
        var bath = TestData.AddService(db, 60, "Baño");
        var consult = TestData.AddService(db, 60, "Consulta");
        TestData.AddAppointment(db, TestData.AddPet(db), bath, TestData.Tomorrow.At(10)); // 10:00–11:00

        var result = await CreateService().GetAvailableSlotsAsync(TestData.Tomorrow, consult.Id);

        var times = Times(result);
        Assert.DoesNotContain("09:30", times); // would end 10:30
        Assert.DoesNotContain("10:00", times);
        Assert.DoesNotContain("10:30", times);
        Assert.Contains("09:00", times);       // ends exactly at 10:00
        Assert.Contains("11:00", times);       // starts exactly at 11:00
    }

    [Fact]
    public async Task Cancelled_appointment_frees_its_slot()
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);
        TestData.AddAppointment(db, TestData.AddPet(db), service, TestData.Tomorrow.At(10), AppointmentStatus.Cancelled);

        var result = await CreateService().GetAvailableSlotsAsync(TestData.Tomorrow, service.Id);

        Assert.Contains("10:00", Times(result));
        Assert.Equal(18, result.Value!.Count);
    }

    [Fact]
    public async Task Appointments_on_other_days_do_not_affect_availability()
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);
        TestData.AddAppointment(db, TestData.AddPet(db), service, TestData.Tomorrow.AddDays(1).At(10));

        var result = await CreateService().GetAvailableSlotsAsync(TestData.Tomorrow, service.Id);

        Assert.Equal(18, result.Value!.Count);
    }

    [Fact]
    public async Task Clinic_is_closed_on_sundays()
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);

        var result = await CreateService().GetAvailableSlotsAsync(TestData.Sunday, service.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("domingos", result.Error);
    }

    [Fact]
    public async Task Past_dates_are_rejected()
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);

        var result = await CreateService().GetAvailableSlotsAsync(TestData.Today.AddDays(-1), service.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("pasadas", result.Error);
    }

    [Fact]
    public async Task Today_only_offers_slots_that_have_not_started()
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);

        var result = await CreateService(now: TestData.Today.At(11, 10))
            .GetAvailableSlotsAsync(TestData.Today, service.Id);

        var times = Times(result);
        Assert.Equal("11:30", times.First());
        Assert.DoesNotContain("11:00", times);
    }

    [Fact]
    public async Task Unknown_service_is_reported()
    {
        var result = await CreateService().GetAvailableSlotsAsync(TestData.Tomorrow, serviceId: 999);

        Assert.True(result.IsFailure);
        Assert.Equal("El servicio seleccionado no existe.", result.Error);
    }

    [Fact]
    public async Task Opening_hours_and_grid_come_from_configuration()
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);
        var options = TestData.Clinic(opening: new TimeOnly(10, 0), closing: new TimeOnly(12, 0), slotMinutes: 15);

        var result = await CreateService(options: options).GetAvailableSlotsAsync(TestData.Tomorrow, service.Id);

        // 10:00, 10:15 … 11:30 (the last one that ends by 12:00)
        Assert.Equal(["10:00", "10:15", "10:30", "10:45", "11:00", "11:15", "11:30"], Times(result));
    }
}
