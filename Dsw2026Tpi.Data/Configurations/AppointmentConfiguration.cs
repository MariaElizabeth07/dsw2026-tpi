using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments");

        builder.Property(a => a.Reason)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.RowVersion)
            .IsRowVersion();

        builder.HasOne(a => a.AvailabilitySlot)
            .WithMany()
            .HasForeignKey(a => a.AvailabilitySlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Patient)
            .WithMany()
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.AvailabilitySlotId)
            .IsUnique()
            .HasFilter("[Status] = 'Booked'");

        builder.HasIndex(a => new { a.PatientId, a.Status });
    }
}
