using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public class SpecialityService : ISpecialityService
{
    private readonly IPersistence _persistence;

    public SpecialityService(IPersistence persistence)
    {
        _persistence = persistence;
    }

    public async Task<Pagination<SpecialityModel.Response>> GetAll(int pageSize, int pageIndex, string? name = null)
    {
        ValidatePagination(pageSize, pageIndex);
        ValidateNameFilter(name);

        var specialities = await _persistence.Paginate<Speciality, string>(
            pageSize,
            pageIndex,
            speciality => string.IsNullOrWhiteSpace(name) || speciality.Name.Contains(name),
            speciality => speciality.Name);

        return specialities.Map(MapResponse);
    }

    public async Task<SpecialityModel.Response> Create(SpecialityModel.Request request)
    {
        ValidateRequest(request);
        await EnsureNameIsUnique(request.Name);

        var speciality = new Speciality(request.Name.Trim(), request.Description.Trim());
        await _persistence.Add(speciality);

        return MapResponse(speciality);
    }

    public async Task<SpecialityModel.Response> Update(Guid id, SpecialityModel.Request request)
    {
        ValidateRequest(request);

        var speciality = await _persistence.GetById<Speciality>(id)
            ?? throw new EntityNotFoundException(nameof(Speciality));

        await EnsureNameIsUnique(request.Name, id);

        speciality.Update(request.Name.Trim(), request.Description.Trim());
        await _persistence.Update(speciality);

        return MapResponse(speciality);
    }

    public async Task Delete(Guid id)
    {
        var speciality = await _persistence.GetById<Speciality>(id)
            ?? throw new EntityNotFoundException(nameof(Speciality));

        speciality.Delete();
        await _persistence.Update(speciality);
    }

    private async Task EnsureNameIsUnique(string name, Guid? specialityId = null)
    {
        var trimmedName = name.Trim();
        var existing = await _persistence.First<Speciality>(
            speciality => speciality.Name == trimmedName && (!specialityId.HasValue || speciality.Id != specialityId.Value));

        if (existing is not null)
        {
            throw new ConflictException(
                nameof(ErrorCodes.SPECIALITY_NAME_ALREADY_EXISTS),
                ErrorCodes.SPECIALITY_NAME_ALREADY_EXISTS)
                .WithDetail(nameof(SpecialityModel.Request.Name), "El nombre de la especialidad ya existe.");
        }
    }

    private static SpecialityModel.Response MapResponse(Speciality speciality)
    {
        return new SpecialityModel.Response(speciality.Id, speciality.Name, speciality.Description);
    }

    private static void ValidatePagination(int pageSize, int pageIndex)
    {
        var exception = new ValidationException();

        if (pageSize <= 0)
        {
            exception.WithDetail(nameof(pageSize), "El pageSize debe ser mayor a 0.");
        }

        if (pageIndex < 0)
        {
            exception.WithDetail(nameof(pageIndex), "El pageIndex debe ser mayor o igual a 0.");
        }

        if (exception.Error.Details.Count != 0)
        {
            throw exception;
        }
    }

    private static void ValidateNameFilter(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length is < 3 or > 100)
        {
            throw new ValidationException()
                .WithDetail(nameof(name), "El filtro name debe tener entre 3 y 100 caracteres.");
        }
    }

    private static void ValidateRequest(SpecialityModel.Request request)
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

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            exception.WithDetail(nameof(request.Description), "La descripción es obligatoria.");
        }
        else if (request.Description.Trim().Length is < 10 or > 100)
        {
            exception.WithDetail(nameof(request.Description), "La descripción debe tener entre 10 y 100 caracteres.");
        }

        if (exception.Error.Details.Count != 0)
        {
            throw exception;
        }
    }
}
