using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Application.Dtos;

public record AppointmentModel
{
    public record PatientRequest(long Dni);
    public record Request(Guid DoctorId, Guid AvailabilitySlotId, PatientRequest Patient, string Reason);
    public record DoctorSummary(Guid Id, string Name);
    public record SlotSummary(Guid Id, DateOnly Date, string StartTime, string EndTime);
    public record Response(Guid Id, DoctorSummary Doctor, SlotSummary Slot, string Reason, string Status, DateTime? CancelledAt);
    public record AdminSpecialtySummary(Guid SpecialtyId, string Name);
    public record AdminDoctorSummary(Guid DoctorId, string Name, AdminSpecialtySummary Specialty);
    public record AdminPatientSummary(long Dni, string FullName);
    public record AdminSummary(Guid AppointmentsId, string AppointmentsStatus, AdminPatientSummary Patient, AdminDoctorSummary Doctor);
}