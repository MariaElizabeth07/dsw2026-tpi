using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.CrossCutting.Helpers;
using Dsw2026Tpi.CrossCutting.Resources;
using Dsw2026Tpi.Data.Identity;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Enums;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IPersistence _persistence;
    private readonly UserManager<ApplicationUser> _userManager;

    public AppointmentService(IPersistence persistence, UserManager<ApplicationUser> userManager)
    {
        _persistence = persistence;
        _userManager = userManager;
    }

    public async Task<AppointmentModel.Response> Create(AppointmentModel.Request request, string authenticatedEmail)
    {
        ValidateRequest(request);

        var currentPatient = await GetAuthenticatedPatient(authenticatedEmail);
        EnsureSameDni(currentPatient, request.Patient.Dni);

        var doctor = await _persistence.GetById<Doctor>(request.DoctorId)
            ?? throw new EntityNotFoundException(nameof(Doctor));

        var slot = await _persistence.GetById<AvailabilitySlot>(request.AvailabilitySlotId)
            ?? throw new EntityNotFoundException(nameof(AvailabilitySlot));

        if (slot.DoctorId != doctor.Id)
        {
            throw new ValidationException()
                .WithDetail(nameof(request.AvailabilitySlotId), "El turno no pertenece al médico indicado.");
        }

        var now = DateTime.Now;
        var slotStart = slot.SlotDate.ToDateTime(slot.StartTime);
        if (slotStart <= now)
        {
            throw new ConflictException(
                nameof(ErrorCodes.APPOINTMENT_SLOT_IN_PAST),
                ErrorCodes.APPOINTMENT_SLOT_IN_PAST);
        }

        if (slot.Status != SlotStatus.Available)
        {
            throw new ConflictException(
                nameof(ErrorCodes.APPOINTMENT_SLOT_NOT_AVAILABLE),
                ErrorCodes.APPOINTMENT_SLOT_NOT_AVAILABLE);
        }

        slot.Book();

        Appointment appointment;
        try
        {
            await _persistence.Update(slot);
            appointment = new Appointment(slot, currentPatient, request.Reason.Trim());
            await _persistence.Add(appointment);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                nameof(ErrorCodes.APPOINTMENT_CONFLICT),
                ErrorCodes.APPOINTMENT_CONFLICT);
        }

        return MapResponse(appointment, doctor, slot);
    }

    public async Task<IReadOnlyCollection<AppointmentModel.Response>> GetByPatient(long dni, string authenticatedEmail)
    {
        if (!dni.IsDNIValid())
        {
            throw new ValidationException().WithDetail(nameof(dni), "El DNI no tiene un formato válido.");
        }

        var currentPatient = await GetAuthenticatedPatient(authenticatedEmail);
        EnsureSameDni(currentPatient, dni);

        var appointments = (await _persistence.GetFiltered<Appointment>(
            appointment => appointment.PatientId == currentPatient.Id && appointment.Status == AppointmentStatus.Booked,
            "AvailabilitySlot.Doctor"))
            ?.ToList() ?? [];

        return appointments
            .OrderBy(appointment => appointment.AvailabilitySlot!.SlotDate)
            .ThenBy(appointment => appointment.AvailabilitySlot!.StartTime)
            .Select(appointment => MapResponse(appointment, appointment.AvailabilitySlot!.Doctor, appointment.AvailabilitySlot!))
            .ToArray();
    }

    public async Task Cancel(Guid id, string authenticatedEmail)
    {
        var currentPatient = await GetAuthenticatedPatient(authenticatedEmail);

        var appointment = await _persistence.GetById<Appointment>(id, nameof(Appointment.AvailabilitySlot))
            ?? throw new EntityNotFoundException(nameof(Appointment));

        if (appointment.PatientId != currentPatient.Id)
        {
            throw new AuthorizationException();
        }

        if (appointment.Status != AppointmentStatus.Booked)
        {
            throw new ConflictException(
                nameof(ErrorCodes.APPOINTMENT_NOT_CANCELABLE),
                ErrorCodes.APPOINTMENT_NOT_CANCELABLE);
        }

        appointment.Cancel();
        await _persistence.Update(appointment);

        var slot = appointment.AvailabilitySlot
            ?? await _persistence.GetById<AvailabilitySlot>(appointment.AvailabilitySlotId)
            ?? throw new EntityNotFoundException(nameof(AvailabilitySlot));

        slot.Release();
        await _persistence.Update(slot);
    }

    private async Task<Patient> GetAuthenticatedPatient(string authenticatedEmail)
    {
        var user = await _userManager.FindByEmailAsync(authenticatedEmail)
            ?? throw new AuthorizationException();

        return await _persistence.First<Patient>(patient => patient.UserId == user.Id)
            ?? throw new AuthorizationException();
    }

    private static void EnsureSameDni(Patient currentPatient, long dni)
    {
        if (!string.Equals(currentPatient.Dni, dni.ToString(), StringComparison.Ordinal))
        {
            throw new AuthorizationException();
        }
    }

    private static AppointmentModel.Response MapResponse(Appointment appointment, Doctor? doctor, AvailabilitySlot slot)
    {
        return new AppointmentModel.Response(
            appointment.Id,
            new AppointmentModel.DoctorSummary(doctor?.Id ?? slot.DoctorId, doctor?.Name ?? string.Empty),
            new AppointmentModel.SlotSummary(slot.Id, slot.SlotDate, slot.StartTime.ToString("HH:mm"), slot.EndTime.ToString("HH:mm")),
            appointment.Reason,
            appointment.Status.ToString().ToUpperInvariant(),
            appointment.CancelledAt);
    }

    private static void ValidateRequest(AppointmentModel.Request request)
    {
        var exception = new ValidationException();

        if (request.DoctorId == Guid.Empty)
        {
            exception.WithDetail(nameof(request.DoctorId), "El doctorId es obligatorio.");
        }

        if (request.AvailabilitySlotId == Guid.Empty)
        {
            exception.WithDetail(nameof(request.AvailabilitySlotId), "El availabilityId es obligatorio.");
        }

        if (request.Patient is null)
        {
            exception.WithDetail(nameof(request.Patient), "El paciente es obligatorio.");
        }
        else if (!request.Patient.Dni.IsDNIValid())
        {
            exception.WithDetail("patient.dni", "El DNI debe tener entre 7 y 10 dígitos.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5)
        {
            exception.WithDetail(nameof(request.Reason), "El motivo debe tener al menos 5 caracteres.");
        }

        if (exception.Error.Details.Count != 0)
        {
            throw exception;
        }
    }
}