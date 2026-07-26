using Dsw2026Tpi.Domain.Enums;

namespace Dsw2026Tpi.Domain.Entities
{
    public class AvailabilitySlot : EntityBase
    {
        public DateOnly SlotDate { get; init; }
        public TimeOnly StartTime { get; init; }
        public TimeOnly EndTime { get; init; }
        public SlotStatus Status { get; private set; }
        public Guid DoctorId { get; init; }
        public Doctor? Doctor { get; private set; }
        public Guid? AvailabilityRuleId { get; init; }
        public AvailabilityRule? AvailabilityRule { get; private set; }
        public bool Deleted { get; private set; }
        public byte[]? RowVersion { get; set; }

        #region Constructor for EF
#pragma warning disable CS8618
        private AvailabilitySlot()
        {
        }
#pragma warning restore CS8618
        #endregion

        public AvailabilitySlot(Doctor doctor, AvailabilityRule? availabilityRule, DateOnly slotDate,
        TimeOnly startTime, TimeOnly endTime, Guid? id = null) : base(id)
        {
            if (startTime >= endTime)
                throw new ArgumentException("La hora de inicio debe ser menor a la hora de fin.");

            Doctor = doctor;
            DoctorId = doctor.Id;
            AvailabilityRule = availabilityRule;
            AvailabilityRuleId = availabilityRule?.Id;
            SlotDate = slotDate;
            StartTime = startTime;
            EndTime = endTime;
            Status = SlotStatus.Available;
            Deleted = false;
        }

        public void Book()
        {
            if (Status != SlotStatus.Available)
                throw new InvalidOperationException("El turno no está disponible para reservar.");
            Status = SlotStatus.Booked;
        }

        public void Release()
        {
            Status = SlotStatus.Available;
        }

        public void Block()
        {
            Status = SlotStatus.Blocked;
        }

        public void Delete()
        {
            Deleted = true;
        }
    }
}