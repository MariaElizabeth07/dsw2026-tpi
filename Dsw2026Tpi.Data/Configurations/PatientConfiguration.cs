using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configurations;


public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");

        builder.Property(p => p.UserId)
            .IsRequired();

        builder.Property(p => p.Dni)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(p => p.Nombre)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Telefono)
            .HasMaxLength(20);

        builder.HasIndex(p => p.UserId)
            .IsUnique();

        builder.HasIndex(p => p.Dni);

        builder.HasQueryFilter(p => !p.Deleted);
    }
}
