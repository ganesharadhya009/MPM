using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.Engagement;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class SurveyConfiguration : IEntityTypeConfiguration<Survey>
{
    public void Configure(EntityTypeBuilder<Survey> builder)
    {
        builder.Property(s => s.Question).IsRequired().HasMaxLength(500);
    }
}

public class SurveyResponseConfiguration : IEntityTypeConfiguration<SurveyResponse>
{
    public void Configure(EntityTypeBuilder<SurveyResponse> builder)
    {
        builder.HasIndex(r => r.SurveyId);
    }
}

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.SerialNo).IsRequired().HasMaxLength(100);
        builder.HasIndex(a => new { a.TenantId, a.SerialNo }).IsUnique();
    }
}

public class HelpdeskTicketConfiguration : IEntityTypeConfiguration<HelpdeskTicket>
{
    public void Configure(EntityTypeBuilder<HelpdeskTicket> builder)
    {
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => new { t.TenantId, t.Status });
    }
}

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.Property(a => a.Title).IsRequired().HasMaxLength(200);
        builder.Property(a => a.AudienceJson).HasColumnType("jsonb");
    }
}
