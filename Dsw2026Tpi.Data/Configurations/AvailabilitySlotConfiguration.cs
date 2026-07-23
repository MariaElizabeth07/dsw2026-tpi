using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dsw2026Tpi.Data.Configurations;

public class AvailabilitySlotConfiguration : IEntityTypeConfiguration<AvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<AvailabilitySlot> builder)
    {
        builder.ToTable("AvailabilitySlots");

        builder.Property(s => s.SlotDate)
            .IsRequired();

        builder.Property(s => s.StartTime)
            .IsRequired();

        builder.Property(s => s.EndTime)
            .IsRequired();

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        builder.HasOne(s => s.Doctor)
            .WithMany()
            .HasForeignKey(s => s.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.AvailabilityRules)
            .WithMany()
            .HasForeignKey(s => s.AvailabilityRulesId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => new { s.DoctorId, s.SlotDate, s.StartTime })
            .IsUnique()
            .HasFilter("[Deleted] = 0");

        builder.HasIndex(s => s.Status);

        builder.HasQueryFilter(s => !s.Deleted);
    }
}
