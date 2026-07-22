namespace Dsw2026Tpi.Domain.Entities
{
    public class Patient : EntityBase
    {
        public string UserId { get; init; }
        public string Dni { get; init; }
        public string Nombre { get; init; }
        public string? Telefono { get; private set; }
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
            Nombre = Name;
            Telefono = phone;
            Deleted = false;
        }

        public void UpdatePhone(string? phone)
        {
            Telefono = phone;
        }

        public void Delete()
        {
            Deleted = true;
        }
    }
}
