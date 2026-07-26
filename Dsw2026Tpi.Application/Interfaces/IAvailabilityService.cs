using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Application.Interfaces;

public interface IAvailabilityService
{
    Task<IReadOnlyCollection<AvailabilityModel.Response>> Create(AvailabilityModel.Request request);
    Task<IReadOnlyCollection<AvailabilityModel.Response>> Update(AvailabilityModel.Request request);
    Task<IReadOnlyCollection<AvailabilityModel.Response>> GetByDoctor(Guid doctorId);
}
