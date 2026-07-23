namespace Dsw2026Tpi.Domain.Entities
{
    public class AvailabilityRules : EntityBase
    {
        public Guid DoctorId { get; init; }
        public Doctor? Doctor { get; private set; }
        public int Month { get; init; }
        public int Year { get; init; }
        public DayOfWeek DayOfWeek { get; init; }
        public TimeSpan StartTime { get; init; }
        public TimeSpan EndTime { get; init; }
        public bool Deleted { get; private set; }

        #region Constructor for EF
#pragma warning disable CS8618
        private AvailabilityRules()
        {
        }
#pragma warning restore CS8618
        #endregion
        public AvailabilityRules(Doctor doctor, int month, int year, DayOfWeek dayOfWeek,
        TimeSpan startTime, TimeSpan endTime, Guid? id = null) : base(id)
        {
            if (startTime >= endTime)
                throw new ArgumentException("La hora de inicio debe ser menor a la hora de fin");

            Doctor = doctor;
            DoctorId = doctor.Id;
            Month = month;
            Year = year;
            DayOfWeek = dayOfWeek;
            StartTime = StartTime;
            EndTime = EndTime;
            Deleted = false;
        }

        public void Delete()
        {
            Deleted = true;
        }
    }
}