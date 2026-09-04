using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.Employees;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.EmployeeCode).IsRequired().HasMaxLength(30);
        builder.HasIndex(e => new { e.TenantId, e.EmployeeCode }).IsUnique();
        builder.HasIndex(e => new { e.TenantId, e.ManagerId });
        builder.HasIndex(e => new { e.TenantId, e.DepartmentId });
        builder.HasIndex(e => new { e.TenantId, e.Status });
    }
}

public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.Property(d => d.BlobUrl).IsRequired();
        builder.HasIndex(d => d.EmployeeId);
    }
}

public class EmployeeSkillConfiguration : IEntityTypeConfiguration<EmployeeSkill>
{
    public void Configure(EntityTypeBuilder<EmployeeSkill> builder)
    {
        builder.Property(s => s.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(s => s.EmployeeId);
    }
}

public class CustomFieldDefinitionConfiguration : IEntityTypeConfiguration<CustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CustomFieldDefinition> builder)
    {
        builder.Property(f => f.Label).IsRequired().HasMaxLength(150);
    }
}

public class EmployeeCustomFieldValueConfiguration : IEntityTypeConfiguration<EmployeeCustomFieldValue>
{
    public void Configure(EntityTypeBuilder<EmployeeCustomFieldValue> builder)
    {
        builder.HasKey(v => new { v.EmployeeId, v.FieldDefinitionId });
    }
}
