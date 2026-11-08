using System.Linq.Expressions;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using NSubstitute;

namespace Dsw2026Tpi.Tests.Unit;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task GetAvailableSlots_CuandoExisteUnTurnoDisponible_EntoncesDevuelveSuIdYHorario()
    {
        var persistence = Substitute.For<IPersistence>();
        var holidayProvider = Substitute.For<IHolidayProvider>();
        var service = new AvailabilityService(persistence, holidayProvider);
        var specialty = new Speciality("Cardiología", "Atención de enfermedades cardíacas.");
        var doctor = new Doctor("Dra. Ana García", "MAT-1234", specialty);
        var expectedSlotId = Guid.NewGuid();
        var expectedDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1));
        var availableSlot = new AvailabilitySlot(
            doctor,
            null,
            expectedDate,
            new TimeOnly(10, 0),
            new TimeOnly(10, 30),
            expectedSlotId);

        persistence.GetById<Doctor>(doctor.Id).Returns(Task.FromResult<Doctor?>(doctor));
        persistence.GetFiltered<AvailabilitySlot>(
                Arg.Any<Expression<Func<AvailabilitySlot, bool>>>(),
                Arg.Any<string[]>())
            .Returns(Task.FromResult<IEnumerable<AvailabilitySlot>?>([availableSlot]));

        var response = await service.GetAvailableSlots(doctor.Id);

        var slot = Assert.Single(response);
        Assert.Equal(expectedSlotId, slot.Id);
        Assert.Equal(expectedDate, slot.Date);
        Assert.Equal("10:00", slot.StartTime);
        Assert.Equal("10:30", slot.EndTime);
    }
}
