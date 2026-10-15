using Hookline.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Hookline.Infrastructure.Persistence.Configurations
{
    public class IncomingEventConfiguration : IEntityTypeConfiguration<IncomingEvent>
    {
        public void Configure(EntityTypeBuilder<IncomingEvent> builder)
        {
            builder.ToTable("incoming_events");
            builder.HasKey(x => x.Id);

            builder.Property(x=>x.Source).HasMaxLength(50).IsRequired(); 
            builder.Property(x=>x.ExternalId).HasMaxLength(200).IsRequired();
            builder.Property(x=>x.EventType).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

            builder.Property(x => x.Headers).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();

            builder.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();

            builder.HasIndex(x => x.ReceivedAt);
            builder.HasIndex(x => x.Status);
        }
    }
}
