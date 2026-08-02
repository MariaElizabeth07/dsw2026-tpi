using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class DoctorService : IDoctorService
{
    private readonly IPersistence _persistence;
    private readonly IAvailabilityService _availabilityService;

    public DoctorService(IPersistence persistence, IAvailabilityService availabilityService)
    {
        _persistence = persistence;
        _availabilityService = availabilityService;
    }

    public async Task<Pagination<DoctorModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
    {
        PaginationValidator.Validate(pageSize, pageIndex);
        ValidateNameFilter(name);

        var doctors = await _persistence.Paginate<Doctor, string>(pageSize,
            pageIndex,
            doctor => string.IsNullOrWhiteSpace(name) || doctor.Name.Contains(name),
            doctor => doctor.Name,
            nameof(Doctor.Speciality));

        return doctors.Map(MapResponse);
    }

    public async Task<DoctorModel.Response> Create(DoctorModel.Request request)
    {
        ValidateRequest(request);

        var speciality = await _persistence.GetById<Speciality>(request.SpecialtyId)
            ?? throw new EntityNotFoundException(nameof(Speciality));

        await EnsureLicenseNumberIsUnique(request.LicenseNumber);

        var doctor = new Doctor(request.Name.Trim(), request.LicenseNumber.Trim(), speciality);
        await _persistence.Add(doctor);

        return MapResponse(doctor);
    }

    public async Task<DoctorModel.Response> Update(Guid id, DoctorModel.Request request)
    {
        ValidateRequest(request);

        var doctor = await _persistence.GetById<Doctor>(id, nameof(Doctor.Speciality))
            ?? throw new EntityNotFoundException(nameof(Doctor));

        var speciality = await _persistence.GetById<Speciality>(request.SpecialtyId)
            ?? throw new EntityNotFoundException(nameof(Speciality));

        await EnsureLicenseNumberIsUnique(request.LicenseNumber, id);

        doctor.Update(request.Name.Trim(), request.LicenseNumber.Trim(), speciality);
        await _persistence.Update(doctor);

        return MapResponse(doctor);
    }

    public async Task Delete(Guid id)
    {
        var doctor = await _persistence.GetById<Doctor>(id)
            ?? throw new EntityNotFoundException(nameof(Doctor));

        doctor.Deactivate();
        await _persistence.Update(doctor);
    }

    public Task<IReadOnlyCollection<AvailabilityModel.Response>> GetAvailabilities(Guid id)
    {
        return _availabilityService.GetByDoctor(id);
    }

    private async Task EnsureLicenseNumberIsUnique(string licenseNumber, Guid? doctorId = null)
    {
        var trimmed = licenseNumber.Trim();
        var existing = await _persistence.First<Doctor>(
            doctor => doctor.LicenseNumber == trimmed && (!doctorId.HasValue || doctor.Id != doctorId.Value));

        if (existing is not null)
        {
            throw new ConflictException(
                nameof(ErrorCodes.DOCTOR_LICENSE_ALREADY_EXISTS),
                ErrorCodes.DOCTOR_LICENSE_ALREADY_EXISTS)
                .WithDetail(nameof(DoctorModel.Request.LicenseNumber), "El número de matrícula ya está en uso.");
        }
    }

    private static DoctorModel.Response MapResponse(Doctor doctor)
    {
        return new DoctorModel.Response(
            doctor.Id,
            doctor.Name,
            doctor.LicenseNumber,
            new DoctorModel.SpecialtyDto(doctor.Speciality?.Id, doctor.Speciality?.Name));
    }
    
    private static void ValidateNameFilter(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (name.Trim().Length is < 3 or > 100)
        {
            throw new ValidationException()
                .WithDetail(nameof(name), "El filtro name debe tener entre 3 y 100 caracteres.");
        }
    }

    private static void ValidateRequest(DoctorModel.Request request)
    {
        var exception = new ValidationException();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            exception.WithDetail(nameof(request.Name), "El nombre es obligatorio.");
        }
        else if (request.Name.Trim().Length is < 3 or > 100)
        {
            exception.WithDetail(nameof(request.Name), "El nombre debe tener entre 3 y 100 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
        {
            exception.WithDetail(nameof(request.LicenseNumber), "El número de matrícula es obligatorio.");
        }
        else if (request.LicenseNumber.Trim().Length is < 4 or > 20)
        {
            exception.WithDetail(nameof(request.LicenseNumber), "El número de matrícula debe tener entre 4 y 20 caracteres.");
        }

        if (request.SpecialtyId == Guid.Empty)
        {
            exception.WithDetail(nameof(request.SpecialtyId), "La especialidad es obligatoria.");
        }

        if (exception.Error.Details.Count != 0)
        {
            throw exception;
        }
    }
}
