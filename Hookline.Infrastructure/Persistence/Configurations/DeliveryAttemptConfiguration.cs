using Hookline.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hookline.Infrastructure.Persistence.Configurations;

public class DeliveryAttemptConfiguration : IEntityTypeConfiguration<DeliveryAttempt>
{
    public void Configure(EntityTypeBuilder<DeliveryAttempt> builder)
    {
        builder.ToTable("delivery_attempts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ResponseBody).HasMaxLength(4000);
        builder.Property(x => x.Error).HasMaxLength(2000);

        // Для воркера: "достань все Pending, у которых NextRetryAt <= now"
        builder.HasIndex(x => new { x.Status, x.NextRetryAt });

        builder.HasOne(x => x.Event)
            .WithMany(e => e.DeliveryAttempts)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Subscriber)
            .WithMany(s => s.DeliveryAttempts)
            .HasForeignKey(x => x.SubscriberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}