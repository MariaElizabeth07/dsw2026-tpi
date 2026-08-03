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
    private readonly IJwtService _jwtService;
    private readonly IPersistence _persistence;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        UserManager<ApplicationUser> userManager,
        ISignInService signInManager,
        IJwtService jwtService,
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

        if (!request.Dni.IsPatientLoginDNIValid())
        {
            throw new ValidationException()
                .WithDetail(nameof(request.Dni), "El DNI no tiene un formato válido.");
        }

        var dni = request.Dni.ToString();
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is not null && user.Deleted)
        {
            _logger.LogInformation("Patient login failed due to user {Email} is deleted.", request.Email);
            throw new AuthenticationException().WithDetail(nameof(request.Email), "El usuario está eliminado.");
        }

        if (user is not null && (user.Dni != dni || (user.Dni == dni && user.Email != request.Email)))
        {
            _logger.LogInformation("Patient login failed due to DNI or Email mismatch for email {Email} and DNI {Dni}.", request.Email, request.Dni);
            throw new AuthenticationException().WithDetail(nameof(request.Email), "El DNI o el email ingresados no coinciden.");
        }

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                Dni = dni,
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

        var patientEntity = await _persistence.First<Patient>(patient => patient.Dni == user.Dni);

        if (patientEntity is null)
        {
            patientEntity = new Patient(user.Id, dni, string.Empty);
            await _persistence.Add<Patient>(patientEntity);
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

        var user = await _userManager.FindByEmailAsync(request.Email) ?? throw new AuthenticationException().WithDetail(nameof(request.Email), "El usuario no existe.");
        if (user.Deleted)
        {
            throw new AuthenticationException().WithDetail(nameof(request.Email), "El usuario está eliminado.");
        }

        var result = await _signInManager.CheckPassword(user, request.Password);
        if (!result)
        {
            _logger.LogWarning("Admin login failed.");
            throw new AuthenticationException().WithDetail(nameof(request.Password), "La contraseña es incorrecta.");
        }

        if (!await _userManager.IsInRoleAsync(user, Roles.Administrator))
        {
            throw new AuthorizationException().WithDetail(nameof(request.Email), "El usuario no tiene permisos suficientes.");
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
