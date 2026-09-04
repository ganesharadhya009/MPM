using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeopleHQ.Domain.Workflow;

namespace PeopleHQ.Infrastructure.Persistence.Configurations;

public class WorkflowRequestConfiguration : IEntityTypeConfiguration<WorkflowRequest>
{
    public void Configure(EntityTypeBuilder<WorkflowRequest> builder)
    {
        builder.Property(r => r.PayloadJson).HasColumnType("jsonb");
        builder.HasIndex(r => new { r.TenantId, r.RequesterEmployeeId, r.Status });
        builder.HasIndex(r => new { r.TenantId, r.RequestType, r.Status });
    }
}

public class WorkflowApprovalStepConfiguration : IEntityTypeConfiguration<WorkflowApprovalStep>
{
    public void Configure(EntityTypeBuilder<WorkflowApprovalStep> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.WorkflowRequestId, s.StepOrder });
        builder.HasIndex(s => new { s.ApproverEmployeeId, s.Status });
    }
}

public class WorkflowChainRuleConfiguration : IEntityTypeConfiguration<WorkflowChainRule>
{
    public void Configure(EntityTypeBuilder<WorkflowChainRule> builder)
    {
        builder.Property(r => r.RuleJson).HasColumnType("jsonb");
        builder.HasIndex(r => new { r.TenantId, r.RequestType, r.Order });
    }
}

public class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> builder)
    {
        builder.HasIndex(d => new { d.FromEmployeeId, d.StartDate, d.EndDate });
    }
}
