using Dsw2026Tpi.Domain.Enums;

namespace Dsw2026Tpi.Domain.Entities
{
    public class Appointment : EntityBase
    {
        public Guid AvailabilitySlotId { get; init; }
        public AvailabilitySlot? AvailabilitySlot { get; private set; }
        public Guid PatientId { get; init; }
        public Patient? Patient { get; private set; }
        public string Reason { get; init; }
        public AppointmentStatus Status { get; private set; }
        public DateTime? CancelledAt { get; private set; }
        public DateTime? AttendedAt { get; private set; }
        public byte[]? RowVersion { get; private set; }

        #region Constructor for EF
#pragma warning disable CS8618
        private Appointment()
        {
        }
#pragma warning restore CS8618
        #endregion

        public Appointment(AvailabilitySlot availabilitySlot, Patient patient, string reason, Guid? id = null) : base(id)
        {
            AvailabilitySlot = availabilitySlot;
            AvailabilitySlotId = availabilitySlot.Id;
            Patient = patient;
            PatientId = patient.Id;
            Reason = reason;
            Status = AppointmentStatus.Booked;
        }

        public void Cancel()
        {
            if (Status != AppointmentStatus.Booked)
                throw new InvalidOperationException("Solo se pueden cancelar citas en estado booked");
            Status = AppointmentStatus.Canceled;
            CancelledAt = DateTime.UtcNow;
        }

        public void Attend()
        {
            if (Status != AppointmentStatus.Booked)
                throw new InvalidOperationException("Solo se pueden marcar como atendidas las citas en estado booked.");
            Status = AppointmentStatus.Attended;
            AttendedAt = DateTime.UtcNow;
        }
        public void MarkNoShow()
        {
            if (Status != AppointmentStatus.Booked)
                throw new InvalidOperationException("Solo se puede marcar ausente una cita en estado booked.");
            Status = AppointmentStatus.NoShow;
        }
    }
}
