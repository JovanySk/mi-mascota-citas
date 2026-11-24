using Microsoft.EntityFrameworkCore;
using MiMascota.Web.Models;
using MiMascota.Web.Services;

namespace MiMascota.Tests;

public class AppointmentServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private AppointmentService CreateService(DateTime? now = null) =>
        new(_database.CreateContext(), TestData.Clinic(), new FixedTimeProvider(now ?? TestData.Now));

    // ---------- Booking ----------

    [Fact]
    public async Task Valid_booking_is_created_as_pending_with_computed_end()
    {
        using var db = _database.CreateContext();
        var pet = TestData.AddPet(db);
        var service = TestData.AddService(db, 60);

        var result = await CreateService().CreateAsync(
            new NewAppointment(pet.Id, service.Id, TestData.Tomorrow.At(10), "  Revisión  "));

        Assert.True(result.IsSuccess);
        var saved = await db.Appointments.SingleAsync();
        Assert.Equal(AppointmentStatus.Pending, saved.Status);
        Assert.Equal(TestData.Tomorrow.At(11), saved.End);
        Assert.Equal("Revisión", saved.Notes);
    }

    [Theory]
    [InlineData(10, 0)]  // same start
    [InlineData(10, 30)] // starts inside the existing appointment
    [InlineData(9, 30)]  // ends inside the existing appointment
    public async Task Overlapping_booking_is_rejected_with_a_descriptive_message(int hour, int minute)
    {
        using var db = _database.CreateContext();
        var pet = TestData.AddPet(db, "Rocky");
        var bath = TestData.AddService(db, 60, "Baño");
        TestData.AddAppointment(db, pet, bath, TestData.Tomorrow.At(10)); // 10:00–11:00

        var result = await CreateService().CreateAsync(
            new NewAppointment(pet.Id, bath.Id, TestData.Tomorrow.At(hour, minute), null));

        Assert.True(result.IsFailure);
        Assert.Contains("ya está ocupado", result.Error);
        Assert.Contains("Rocky (Baño, 10:00–11:00)", result.Error);
        Assert.Equal(1, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task Back_to_back_booking_is_allowed()
    {
        using var db = _database.CreateContext();
        var pet = TestData.AddPet(db);
        var service = TestData.AddService(db, 60);
        TestData.AddAppointment(db, pet, service, TestData.Tomorrow.At(10));

        var result = await CreateService().CreateAsync(
            new NewAppointment(pet.Id, service.Id, TestData.Tomorrow.At(11), null));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Slot_of_a_cancelled_appointment_can_be_booked_again()
    {
        using var db = _database.CreateContext();
        var pet = TestData.AddPet(db);
        var service = TestData.AddService(db, 30);
        TestData.AddAppointment(db, pet, service, TestData.Tomorrow.At(10), AppointmentStatus.Cancelled);

        var result = await CreateService().CreateAsync(
            new NewAppointment(pet.Id, service.Id, TestData.Tomorrow.At(10), null));

        Assert.True(result.IsSuccess);
    }

    public static TheoryData<DateTime, string> InvalidStarts => new()
    {
        { TestData.Today.At(7, 30), "ya pasó" },              // before "now"
        { TestData.Sunday.At(10), "no abre los domingos" },   // closed day
        { TestData.Tomorrow.At(8, 30), "horario de atención" }, // before opening
        { TestData.Tomorrow.At(17, 30), "horario de atención" }, // 60 min would end 18:30
        { TestData.Tomorrow.At(10, 10), "intervalos de 30 minutos" } // off the grid
    };

    [Theory]
    [MemberData(nameof(InvalidStarts))]
    public async Task Booking_outside_the_schedule_is_rejected(DateTime start, string expectedMessage)
    {
        using var db = _database.CreateContext();
        var pet = TestData.AddPet(db);
        var service = TestData.AddService(db, 60);

        var result = await CreateService().CreateAsync(new NewAppointment(pet.Id, service.Id, start, null));

        Assert.True(result.IsFailure);
        Assert.Contains(expectedMessage, result.Error);
    }

    [Fact]
    public async Task Booking_for_an_unknown_pet_is_rejected()
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);

        var result = await CreateService().CreateAsync(
            new NewAppointment(PetId: 999, service.Id, TestData.Tomorrow.At(10), null));

        Assert.Equal("La mascota seleccionada no existe.", result.Error);
    }

    // ---------- State machine ----------

    [Theory]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Cancelled)]
    public async Task Valid_transitions_are_saved(AppointmentStatus from, AppointmentStatus to)
    {
        var id = SeedAppointment(from);

        var result = await CreateService().ChangeStatusAsync(id, to);

        Assert.True(result.IsSuccess);
        Assert.Equal(to, await StatusOf(id));
    }

    [Theory]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Pending, AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Confirmed, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Attended, AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Attended, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Attended, AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.Attended, AppointmentStatus.Cancelled)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Pending)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Confirmed)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Attended)]
    [InlineData(AppointmentStatus.Cancelled, AppointmentStatus.Cancelled)]
    public async Task Invalid_transitions_are_rejected_and_nothing_changes(AppointmentStatus from, AppointmentStatus to)
    {
        var id = SeedAppointment(from);

        var result = await CreateService().ChangeStatusAsync(id, to);

        Assert.True(result.IsFailure);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.Equal(AppointmentStateMachine.DescribeRejection(from, to), result.Error);
        Assert.Equal(from, await StatusOf(id));
    }

    [Fact]
    public void Rejection_messages_explain_the_reason_in_spanish()
    {
        Assert.Equal(
            "No se puede marcar como atendida una cita pendiente: primero debe confirmarse.",
            AppointmentStateMachine.DescribeRejection(AppointmentStatus.Pending, AppointmentStatus.Attended));
        Assert.Contains("ya fue atendida",
            AppointmentStateMachine.DescribeRejection(AppointmentStatus.Attended, AppointmentStatus.Cancelled));
        Assert.Contains("está cancelada",
            AppointmentStateMachine.DescribeRejection(AppointmentStatus.Cancelled, AppointmentStatus.Confirmed));
    }

    [Fact]
    public async Task Cannot_confirm_an_appointment_that_already_started()
    {
        var id = SeedAppointment(AppointmentStatus.Pending, start: TestData.Tomorrow.At(10));

        // The clock is now after the appointment's start time.
        var result = await CreateService(now: TestData.Tomorrow.At(10, 15)).ChangeStatusAsync(id, AppointmentStatus.Confirmed);

        Assert.True(result.IsFailure);
        Assert.Contains("ya pasaron", result.Error);
        Assert.Equal(AppointmentStatus.Pending, await StatusOf(id));
    }

    [Fact]
    public async Task Changing_the_status_of_a_missing_appointment_fails()
    {
        var result = await CreateService().ChangeStatusAsync(appointmentId: 999, AppointmentStatus.Confirmed);

        Assert.Equal("La cita no existe.", result.Error);
    }

    private int SeedAppointment(AppointmentStatus status, DateTime? start = null)
    {
        using var db = _database.CreateContext();
        var service = TestData.AddService(db, 30);
        return TestData.AddAppointment(db, TestData.AddPet(db), service, start ?? TestData.Tomorrow.At(10), status).Id;
    }

    private async Task<AppointmentStatus> StatusOf(int id)
    {
        using var db = _database.CreateContext();
        return (await db.Appointments.SingleAsync(a => a.Id == id)).Status;
    }
}
