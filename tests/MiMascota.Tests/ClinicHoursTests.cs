using MiMascota.Web.Options;
using MiMascota.Web.Services;

namespace MiMascota.Tests;

/// <summary>
/// "Abierto ahora / Cerrado" is computed in the clinic's zone (America/Mexico_City, UTC-6 all year),
/// so every instant below is written in UTC and converted by the service.
/// 2026-11-24 is a Tuesday; the clinic opens Monday to Saturday 09:00–18:00.
/// </summary>
public class ClinicHoursTests
{
    private static ClinicHours Create(ClinicOptions? options = null, DateTimeOffset? now = null) =>
        new(Microsoft.Extensions.Options.Options.Create(options ?? TestData.Clinic().Value),
            new FixedTimeProvider((now ?? Utc(2026, 11, 24, 18, 0)).UtcDateTime));

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(15, 0)]   // 09:00 local: opening time is open
    [InlineData(18, 30)]  // 12:30 local
    [InlineData(23, 59)]  // 17:59 local
    public void Open_during_opening_hours(int utcHour, int utcMinute)
    {
        var status = Create().GetStatus(Utc(2026, 11, 24, utcHour, utcMinute));

        Assert.True(status.IsOpen);
        Assert.Equal("Abierto ahora", status.Label);
        Assert.Equal("Cerramos hoy a las 18:00.", status.Detail);
    }

    [Fact]
    public void Closed_before_opening_says_it_opens_today()
    {
        var status = Create().GetStatus(Utc(2026, 11, 24, 14, 59)); // 08:59 local

        Assert.False(status.IsOpen);
        Assert.Equal("Cerrado", status.Label);
        Assert.Equal("Abrimos hoy a las 9:00.", status.Detail);
    }

    [Fact]
    public void Closing_time_itself_is_already_closed()
    {
        var status = Create().GetStatus(Utc(2026, 11, 25, 0, 0)); // Tuesday 18:00 local

        Assert.False(status.IsOpen);
        Assert.Equal("Abrimos mañana a las 9:00.", status.Detail);
    }

    [Fact]
    public void Uses_the_clinic_time_zone_not_utc()
    {
        // 03:00 UTC on Wednesday is still Tuesday 21:00 in Tantoyuca.
        var status = Create().GetStatus(Utc(2026, 11, 25, 3, 0));

        Assert.False(status.IsOpen);
        Assert.Equal(new DateTime(2026, 11, 24, 21, 0, 0), status.ClinicNow);
        Assert.Equal(DayOfWeek.Tuesday, status.ClinicNow.DayOfWeek);
    }

    [Fact]
    public void Sunday_is_closed_and_points_to_monday()
    {
        var status = Create().GetStatus(Utc(2026, 11, 29, 18, 0)); // Sunday 12:00 local

        Assert.False(status.IsOpen);
        Assert.Equal("Abrimos mañana a las 9:00.", status.Detail);
    }

    [Fact]
    public void Saturday_evening_points_to_monday_by_name()
    {
        var status = Create().GetStatus(Utc(2026, 11, 29, 1, 0)); // Saturday 19:00 local

        Assert.False(status.IsOpen);
        Assert.Equal("Abrimos el lunes a las 9:00.", status.Detail);
    }

    [Fact]
    public void Hours_come_from_configuration()
    {
        var options = new ClinicOptions
        {
            OpeningTime = new TimeOnly(10, 0),
            ClosingTime = new TimeOnly(14, 0),
            ClosedDays = [DayOfWeek.Sunday, DayOfWeek.Monday]
        };
        var hours = Create(options);

        Assert.False(hours.GetStatus(Utc(2026, 11, 24, 15, 30)).IsOpen);        // 09:30 local
        Assert.True(hours.GetStatus(Utc(2026, 11, 24, 16, 0)).IsOpen);          // 10:00 local
        Assert.False(hours.GetStatus(Utc(2026, 11, 24, 20, 0)).IsOpen);         // 14:00 local
        Assert.False(hours.GetStatus(Utc(2026, 11, 23, 17, 0)).IsOpen);         // Monday 11:00, closed day
        Assert.Equal("Abrimos el martes a las 10:00.", hours.GetStatus(Utc(2026, 11, 29, 2, 0)).Detail); // Saturday 20:00
    }

    [Fact]
    public void A_clinic_with_no_open_days_never_opens()
    {
        var options = new ClinicOptions { ClosedDays = Enum.GetValues<DayOfWeek>().ToList() };

        var status = Create(options).GetStatus(Utc(2026, 11, 24, 18, 0));

        Assert.False(status.IsOpen);
        Assert.Equal("Por ahora no tenemos días de atención.", status.Detail);
    }

    [Fact]
    public void Without_an_explicit_instant_it_uses_the_injected_clock()
    {
        var hours = Create(now: Utc(2026, 11, 24, 18, 0)); // Tuesday 12:00 local

        Assert.True(hours.GetStatus().IsOpen);
        Assert.Equal(DayOfWeek.Tuesday, hours.Week().Single(d => d.IsToday).Day);
    }

    [Fact]
    public void Week_starts_on_monday_and_marks_closed_days_and_today()
    {
        var week = Create().Week(Utc(2026, 11, 29, 18, 0)); // Sunday

        Assert.Equal(7, week.Count);
        Assert.Equal("Lunes", week[0].Name);
        Assert.Equal("Sábado", week[5].Name);
        Assert.Equal("Domingo", week[6].Name);
        Assert.True(week[6].IsClosed);
        Assert.True(week[6].IsToday);
        Assert.Single(week, d => d.IsToday);
        Assert.All(week.Take(6), d => Assert.False(d.IsClosed));
    }

    [Fact]
    public void Slots_per_day_follow_the_grid()
    {
        var week = Create().Week(Utc(2026, 11, 24, 18, 0));

        Assert.Equal(18, week[0].Slots(30)); // 09:00–18:00 every 30 minutes
        Assert.Equal(9, week[0].Slots(60));
        Assert.Equal(0, week[6].Slots(30));  // Sunday
    }

    [Fact]
    public void Unknown_time_zone_falls_back_to_central_mexico()
    {
        var options = new ClinicOptions { TimeZoneId = "Not/AZone", ClosedDays = [DayOfWeek.Sunday] };

        var status = Create(options).GetStatus(Utc(2026, 11, 24, 15, 0));

        Assert.True(status.IsOpen);
        Assert.Equal(new DateTime(2026, 11, 24, 9, 0, 0), status.ClinicNow);
    }
}
