using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.Performance;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.Property(g => g.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(g => g.EmployeeId);
    }
}

public class OkrCycleConfiguration : IEntityTypeConfiguration<OkrCycle>
{
    public void Configure(EntityTypeBuilder<OkrCycle> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
    }
}

public class ObjectiveConfiguration : IEntityTypeConfiguration<Objective>
{
    public void Configure(EntityTypeBuilder<Objective> builder)
    {
        builder.Property(o => o.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(o => o.CycleId);
        builder.HasIndex(o => o.ParentObjectiveId);
    }
}

public class KeyResultConfiguration : IEntityTypeConfiguration<KeyResult>
{
    public void Configure(EntityTypeBuilder<KeyResult> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Title).IsRequired().HasMaxLength(200);
        builder.Property(k => k.StartValue).HasColumnType("numeric(14,2)");
        builder.Property(k => k.TargetValue).HasColumnType("numeric(14,2)");
        builder.Property(k => k.CurrentValue).HasColumnType("numeric(14,2)");
        builder.HasIndex(k => k.ObjectiveId);
    }
}

public class FeedbackNoteConfiguration : IEntityTypeConfiguration<FeedbackNote>
{
    public void Configure(EntityTypeBuilder<FeedbackNote> builder)
    {
        builder.Property(f => f.Message).IsRequired();
        builder.HasIndex(f => f.ToEmployeeId);
    }
}
