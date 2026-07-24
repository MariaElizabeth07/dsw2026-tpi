namespace Dsw2026Tpi.Domain.Entities
{
    public class Patient : EntityBase
    {
        public string UserId { get; init; }
        public string Dni { get; init; }
        public string FullName { get; init; }
        public string? Phone { get; private set; }
        public bool Deleted { get; private set; }

        #region Constructor for EF
#pragma warning disable CS8618
        private Patient()
        {
        }
#pragma warning restore CS8618
        #endregion
        public Patient(string userId, string dni, string Name, string? phone = null, Guid? id = null) : base(id)
        {
            UserId = userId;
            Dni = dni;
            FullName = Name;
            Phone = phone;
            Deleted = false;
        }

        public void UpdatePhone(string? phone)
        {
            Phone = phone;
        }

        public void Delete()
        {
            Deleted = true;
        }
    }
}
