namespace Dsw2026Tpi.Application.Dtos;

public record AvailabilityModel
{
    public record Request(Guid DoctorId, IReadOnlyCollection<DayRequest>? Days);
    public record DayRequest(string Day, string StartTime, string EndTime);
    public record Response(Guid Id, string Day, string StartTime, string EndTime);
}
