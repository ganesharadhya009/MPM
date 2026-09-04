using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.Onboarding;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(c => new { c.TenantId, c.Stage });
    }
}

public class OnboardingChecklistTemplateConfiguration : IEntityTypeConfiguration<OnboardingChecklistTemplate>
{
    public void Configure(EntityTypeBuilder<OnboardingChecklistTemplate> builder)
    {
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
    }
}

public class OnboardingChecklistItemConfiguration : IEntityTypeConfiguration<OnboardingChecklistItem>
{
    public void Configure(EntityTypeBuilder<OnboardingChecklistItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Title).IsRequired().HasMaxLength(200);
        builder.Property(i => i.OwnerRole).IsRequired().HasMaxLength(50);
        builder.HasIndex(i => i.TemplateId);
    }
}

public class OnboardingTaskConfiguration : IEntityTypeConfiguration<OnboardingTask>
{
    public void Configure(EntityTypeBuilder<OnboardingTask> builder)
    {
        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(t => t.EmployeeId);
        builder.HasIndex(t => t.CandidateId);
    }
}
