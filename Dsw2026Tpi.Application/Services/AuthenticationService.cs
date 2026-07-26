using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Dsw2026Tpi.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignInService _signInManager;
    private readonly JwtService _jwtService;
    private readonly IPersistence _persistence;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        ISignInService signInManager,
        JwtService jwtService,
        IPersistence persistence,
        ILogger<AuthenticationService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _persistence = persistence;
        _logger = logger;
    }

    public async Task<LoginPatientModel.Response> LoginPatient(LoginPatientModel.Request request)
    {
        if (!request.Email.IsEmailValid())
        {
            throw new ValidationException()
                .WithDetail(nameof(request.Email), "El email no tiene un formato válido.");
        }

        if (!request.Dni.IsDNIValid())
        {
            throw new ValidationException()
                .WithDetail(nameof(request.Dni), "El DNI no tiene un formato válido.");
        }

        var dni = request.Dni.ToString();
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is not null && user.Deleted)
        {
            throw new AuthenticationException();
        }

        var patientByUser = user is null
            ? null
            : await _persistence.First<Patient>(p => p.UserId == user.Id);
        var patientByDni = await _persistence.First<Patient>(p => p.Dni == dni);

        if (patientByDni is not null && (patientByUser is null || patientByDni.UserId != patientByUser.UserId))
        {
            throw new ConflictException(nameof(ErrorCodes.PATIENT_DNI_ALREADY_EXISTS), ErrorCodes.PATIENT_DNI_ALREADY_EXISTS)
                .WithDetail(nameof(request.Dni), "El DNI ya está asociado a otro paciente.");
        }

        if (patientByUser is not null && !string.Equals(patientByUser.Dni, dni, StringComparison.Ordinal))
        {
            _logger.LogWarning("Patient login failed due to DNI mismatch for email {Email}.", request.Email);
            throw new AuthenticationException();
        }

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createUserResult = await _userManager.CreateAsync(user);
            if (!createUserResult.Succeeded)
            {
                throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT), ErrorCodes.REGISTER_USER_CONFLICT)
                    .WithDetail(createUserResult.Errors.Select(error => (error.Code, error.Description)));
            }
        }

        var patient = patientByUser;
        if (patient is null)
        {
            patient = new Patient(user.Id, dni, request.Email);
            await _persistence.Add(patient);
        }

        if (!await _userManager.IsInRoleAsync(user, Roles.Patient))
        {
            var addToRoleResult = await _userManager.AddToRoleAsync(user, Roles.Patient);
            if (!addToRoleResult.Succeeded)
            {
                throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT), ErrorCodes.REGISTER_USER_CONFLICT)
                    .WithDetail(addToRoleResult.Errors.Select(error => (error.Code, error.Description)));
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var token = _jwtService.GenerateToken(user, Roles.Patient);
        return new LoginPatientModel.Response(token, Roles.Patient.ToUpperInvariant());
    }

    public async Task<LoginAdminModel.Response> LoginAdmin(LoginAdminModel.Request request)
    {
        if (!request.Email.IsEmailValid())
        {
            throw new ValidationException()
                .WithDetail(nameof(request.Email), "El email no tiene un formato válido.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ValidationException()
                .WithDetail(nameof(request.Password), "La contraseña debe tener al menos 8 caracteres.");
        }

        var user = await _userManager.FindByEmailAsync(request.Email) ?? throw new AuthenticationException();
        if (user.Deleted)
        {
            throw new AuthenticationException();
        }

        var result = await _signInManager.CheckPassword(user, request.Password);
        if (!result)
        {
            _logger.LogWarning("Admin login failed.");
            throw new AuthenticationException();
        }

        if (!await _userManager.IsInRoleAsync(user, Roles.Administrator))
        {
            throw new AuthorizationException();
        }

        var token = _jwtService.GenerateToken(user, Roles.Administrator);
        return new LoginAdminModel.Response(token, Roles.Administrator.ToUpperInvariant());
    }

    public async Task<RegisterModel.Response> Register(RegisterModel.Request request)
    {
        if (!request.Email.IsEmailValid())
        {
            throw new ValidationException(ErrorCodes.REGISTER_USER_INVALID, nameof(ErrorCodes.REGISTER_USER_INVALID));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new ConflictException(nameof(ErrorCodes.REGISTER_USER_CONFLICT), ErrorCodes.REGISTER_USER_CONFLICT)
                .WithDetail(result.Errors.Select(error => (error.Code, error.Description)));
        }

        _ = await _userManager.AddToRoleAsync(user, Roles.Administrator);

        _logger.LogInformation("Administrator user registered.");

        return new RegisterModel.Response(request.Email);
    }
}
