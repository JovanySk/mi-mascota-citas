namespace MiMascota.Web.Services;

public interface IAvailabilityService
{
    /// <summary>
    /// Free start times for a service on a given date. Fails (with a Spanish message)
    /// when the service does not exist, the date is in the past or the clinic is closed.
    /// </summary>
    Task<Result<IReadOnlyList<TimeSlot>>> GetAvailableSlotsAsync(DateOnly date, int serviceId);
}

public record TimeSlot(DateTime Start, DateTime End)
{
    public string Label => $"{Start:HH:mm} – {End:HH:mm}";
}
