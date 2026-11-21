using System.Linq.Expressions;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Dsw2026Tpi.Tests.Unit;

public class AppointmentServiceTests
{
    [Theory]
    [InlineData(false, "", "patient@test.com")]
    [InlineData(true, "", "patient@test.com")]
    [InlineData(false, "Ana Perez", "patient@test.com")]
    [InlineData(true, "Ana Perez", "patient@test.com")]
    [InlineData(false, "old@test.com", "patient@test.com")]
    [InlineData(true, "old@test.com", "patient@test.com")]
    [InlineData(false, "", null)]
    [InlineData(true, "", null)]
    public async Task ListadoAdmin_DevuelveNombreOriginalYEmailDelUsuarioAsociado(bool byDate, string fullName, string? email)
    {
        var persistence = Substitute.For<IPersistence>();
        var userManager = CreateUserManager();
        var service = new AppointmentService(persistence, userManager);
        var patient = new Patient("patient-id", "12345678", fullName);
        var specialty = new Speciality("Cardiologia", "Atencion cardiaca");
        var doctor = new Doctor("Dra. Ana", "MAT-123", specialty);
        var slot = new AvailabilitySlot(doctor, null, new DateOnly(2026, 12, 1),
            new TimeOnly(10, 0), new TimeOnly(10, 30));
        var firstAppointment = new Appointment(slot, patient, "Consulta");
        var secondAppointment = new Appointment(slot, patient, "Control");
        var page = new Pagination<Appointment>(10, 0, 12, [firstAppointment, secondAppointment]);
        persistence.Paginate<Appointment, DateOnly>(
                10, 0, Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<Expression<Func<Appointment, DateOnly>>>(), Arg.Any<string[]>())
            .Returns(Task.FromResult(page));
        userManager.FindByIdAsync(patient.UserId).Returns(Task.FromResult<ApplicationUser?>(
            email is null ? null : new ApplicationUser { Id = patient.UserId, Email = email }));

        var result = byDate
            ? await service.GetByDate(new DateOnly(2026, 12, 1), 10, 0)
            : await service.Search(10, 0, null, null, null, null);

        Assert.Equal(10, result.PageSize);
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(12, result.Total);
        Assert.Equal(2, result.Data.Count());
        Assert.All(result.Data, appointment =>
        {
            Assert.Equal(fullName, appointment.Patient.FullName);
            Assert.Equal(email, appointment.Patient.Email);
            Assert.Equal(12345678L, appointment.Patient.Dni);
        });
        Assert.Equal(firstAppointment.Id, result.Data.First().AppointmentsId);
        Assert.Equal(secondAppointment.Id, result.Data.Last().AppointmentsId);
        await userManager.Received(1).FindByIdAsync(patient.UserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListadoAdmin_SinTurnos_NoConsultaUsuarios(bool byDate)
    {
        var persistence = Substitute.For<IPersistence>();
        var userManager = CreateUserManager();
        var service = new AppointmentService(persistence, userManager);
        persistence.Paginate<Appointment, DateOnly>(
                10, 0, Arg.Any<Expression<Func<Appointment, bool>>>(),
                Arg.Any<Expression<Func<Appointment, DateOnly>>>(), Arg.Any<string[]>())
            .Returns(Task.FromResult(new Pagination<Appointment>(10, 0, 0, [])));

        var result = byDate
            ? await service.GetByDate(new DateOnly(2026, 12, 1), 10, 0)
            : await service.Search(10, 0, null, null, null, null);

        Assert.Empty(result.Data);
        await userManager.DidNotReceive().FindByIdAsync(Arg.Any<string>());
    }

    private static UserManager<ApplicationUser> CreateUserManager()
    {
        var options = Substitute.For<IOptions<IdentityOptions>>();
        options.Value.Returns(new IdentityOptions());
        return Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), options,
            Substitute.For<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Substitute.For<ILookupNormalizer>(), Substitute.For<IdentityErrorDescriber>(),
            Substitute.For<IServiceProvider>(), Substitute.For<ILogger<UserManager<ApplicationUser>>>());
    }
}
