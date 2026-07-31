using System.Linq.Expressions;
using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Dsw2026Tpi.Tests.Unit;

public class AuthenticationServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignInService _signInService;
    private readonly IJwtService _jwtService;
    private readonly IPersistence _persistence;
    private readonly AuthenticationService _service;

    public AuthenticationServiceTests()
    {
        _userManager = CreateUserManager();
        _signInService = Substitute.For<ISignInService>();
        _jwtService = Substitute.For<IJwtService>();
        _persistence = Substitute.For<IPersistence>();

        _service = new AuthenticationService(
            _userManager,
            _signInService,
            _jwtService,
            _persistence,
            Substitute.For<ILogger<AuthenticationService>>());
    }

    [Fact]
    public async Task LoginAdmin_CuandoLasCredencialesSonValidas_EntoncesDevuelveTokenYRolAdministrador()
    {
        // Arrange
        var request = new LoginAdminModel.Request("admin@test.com", "Password123");
        var user = new ApplicationUser { Id = "admin-id", Email = request.Email };

        _userManager.FindByEmailAsync(request.Email).Returns(Task.FromResult<ApplicationUser?>(user));
        _signInService.CheckPassword(user, request.Password).Returns(Task.FromResult(true));
        _userManager.IsInRoleAsync(user, Roles.Administrator).Returns(Task.FromResult(true));
        _jwtService.GenerateToken(user, Roles.Administrator).Returns("admin-token");

        // Act
        var response = await _service.LoginAdmin(request);

        // Assert
        Assert.Equal("admin-token", response.Token);
        Assert.Equal(Roles.Administrator.ToUpperInvariant(), response.Role);
    }

    [Fact]
    public async Task LoginAdmin_CuandoLaPasswordEsIncorrecta_EntoncesLanzaAuthenticationException()
    {
        // Arrange
        var request = new LoginAdminModel.Request("admin@test.com", "Password123");
        var user = new ApplicationUser { Id = "admin-id", Email = request.Email };

        _userManager.FindByEmailAsync(request.Email).Returns(Task.FromResult<ApplicationUser?>(user));
        _signInService.CheckPassword(user, request.Password).Returns(Task.FromResult(false));

        // Act
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => _service.LoginAdmin(request));

        // Assert
        Assert.NotNull(exception);
        _jwtService.DidNotReceive().GenerateToken(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginPatient_CuandoElUsuarioNoExiste_EntoncesCreaUsuarioPacienteYDevuelveToken()
    {
        // Arrange
        var request = new LoginPatientModel.Request("paciente@test.com", 12345678);

        _userManager.FindByEmailAsync(request.Email).Returns(Task.FromResult<ApplicationUser?>(null));
        _userManager.CreateAsync(Arg.Any<ApplicationUser>()).Returns(Task.FromResult(IdentityResult.Success));
        _persistence.First(Arg.Any<Expression<Func<Patient, bool>>>(), Arg.Any<string[]>())
            .Returns(Task.FromResult<Patient>(null!));
        _persistence.Add(Arg.Any<Patient>()).Returns(call => Task.FromResult(call.ArgAt<Patient>(0)));
        _userManager.IsInRoleAsync(Arg.Any<ApplicationUser>(), Roles.Patient).Returns(Task.FromResult(false));
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Patient).Returns(Task.FromResult(IdentityResult.Success));
        _userManager.UpdateAsync(Arg.Any<ApplicationUser>()).Returns(Task.FromResult(IdentityResult.Success));
        _jwtService.GenerateToken(Arg.Any<ApplicationUser>(), Roles.Patient).Returns("patient-token");

        // Act
        var response = await _service.LoginPatient(request);

        // Assert
        Assert.Equal("patient-token", response.Token);
        Assert.Equal(Roles.Patient.ToUpperInvariant(), response.Role);
        await _userManager.Received(1).CreateAsync(Arg.Is<ApplicationUser>(user =>
            user != null && user.Email == request.Email && user.Dni == request.Dni.ToString()));
        await _persistence.Received(1).Add(Arg.Is<Patient>(patient =>
            patient != null && patient.Dni == request.Dni.ToString() && patient.FullName == request.Email));
        await _userManager.Received(1).AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Patient);
    }

    [Fact]
    public async Task LoginPatient_CuandoElEmailExistePeroNoCoincideElDni_EntoncesLanzaAuthenticationException()
    {
        // Arrange
        var request = new LoginPatientModel.Request("paciente@test.com", 12345678);
        var user = new ApplicationUser
        {
            Id = "patient-id",
            Email = request.Email,
            Dni = "87654321"
        };

        _userManager.FindByEmailAsync(request.Email).Returns(Task.FromResult<ApplicationUser?>(user));

        // Act
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => _service.LoginPatient(request));

        // Assert
        Assert.NotNull(exception);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>());
        await _persistence.DidNotReceive().Add(Arg.Any<Patient>());
        _jwtService.DidNotReceive().GenerateToken(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginPatient_CuandoElUsuarioExistePeroEstaEliminado_EntoncesLanzaAuthenticationException()
    {
        // Arrange
        var request = new LoginPatientModel.Request("paciente@test.com", 12345678);
        var user = new ApplicationUser
        {
            Id = "patient-id",
            Email = request.Email,
            Dni = request.Dni.ToString(),
            Deleted = true
        };

        _userManager.FindByEmailAsync(request.Email).Returns(Task.FromResult<ApplicationUser?>(user));

        // Act
        var exception = await Assert.ThrowsAsync<AuthenticationException>(() => _service.LoginPatient(request));

        // Assert
        Assert.NotNull(exception);
        await _userManager.DidNotReceive().UpdateAsync(Arg.Any<ApplicationUser>());
        _jwtService.DidNotReceive().GenerateToken(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Register_CuandoLosDatosSonValidos_EntoncesCreaAdministrador()
    {
        // Arrange
        var request = new RegisterModel.Request("admin@test.com", "Password123");

        _userManager.CreateAsync(Arg.Any<ApplicationUser>(), request.Password).Returns(Task.FromResult(IdentityResult.Success));
        _userManager.AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Administrator).Returns(Task.FromResult(IdentityResult.Success));

        // Act
        var response = await _service.Register(request);

        // Assert
        Assert.Equal(request.Email, response.Email);
        await _userManager.Received(1).CreateAsync(Arg.Is<ApplicationUser>(user =>
            user != null && user.Email == request.Email && user.UserName == request.Email), request.Password);
        await _userManager.Received(1).AddToRoleAsync(Arg.Any<ApplicationUser>(), Roles.Administrator);
    }

    private static UserManager<ApplicationUser> CreateUserManager()
    {
        var options = Substitute.For<IOptions<IdentityOptions>>();
        options.Value.Returns(new IdentityOptions());

        return Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            options,
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(),
            Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<ApplicationUser>>>());
    }
}
