using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Domain.Attendance;
using PeopleHQ.Domain.Auditing;
using PeopleHQ.Domain.Dashboards;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Domain.Engagement;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Domain.Integrations;
using PeopleHQ.Domain.Leave;
using PeopleHQ.Domain.Notifications;
using PeopleHQ.Domain.Onboarding;
using PeopleHQ.Domain.OrgStructure;
using PeopleHQ.Domain.Payroll;
using PeopleHQ.Domain.Performance;
using PeopleHQ.Domain.Tenancy;
using PeopleHQ.Domain.Timesheet;
using PeopleHQ.Domain.Workflow;
using PeopleHQ.Infrastructure.Persistence.Configurations;

namespace PeopleHQ.Infrastructure.Persistence;

/// <summary>
/// Composition root for the whole schema (02-data-model-erd.md). Identity
/// plumbing comes from IdentityUserContext (users only — Roles/Permissions
/// below are the custom model per 00-overview.md §5, not ASP.NET Identity's
/// IdentityRole). Every tenant-owned DbSet is protected by the reflection-based
/// global query filter applied in OnModelCreating — see TenantQueryFilterApplier.
/// </summary>
public class AppDbContext : IdentityUserContext<AppUser, Guid>
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Platform
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Plan> Plans => Set<Plan>();

    // Identity (custom Role/Permission model)
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Org Structure
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<HolidayCalendar> HolidayCalendars => Set<HolidayCalendar>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<EmployeePositionHistory> EmployeePositionHistories => Set<EmployeePositionHistory>();

    // Employees
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeSkill> EmployeeSkills => Set<EmployeeSkill>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<EmployeeCustomFieldValue> EmployeeCustomFieldValues => Set<EmployeeCustomFieldValue>();

    // Onboarding
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<OnboardingChecklistTemplate> OnboardingChecklistTemplates => Set<OnboardingChecklistTemplate>();
    public DbSet<OnboardingChecklistItem> OnboardingChecklistItems => Set<OnboardingChecklistItem>();
    public DbSet<OnboardingTask> OnboardingTasks => Set<OnboardingTask>();

    // Leave
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<LeaveTypePolicyRule> LeaveTypePolicyRules => Set<LeaveTypePolicyRule>();
    public DbSet<EmployeeLeavePolicy> EmployeeLeavePolicies => Set<EmployeeLeavePolicy>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveBlackoutPeriod> LeaveBlackoutPeriods => Set<LeaveBlackoutPeriod>();

    // Attendance
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceRegularizationRequest> AttendanceRegularizationRequests => Set<AttendanceRegularizationRequest>();

    // Workflow Engine
    public DbSet<WorkflowRequest> WorkflowRequests => Set<WorkflowRequest>();
    public DbSet<WorkflowApprovalStep> WorkflowApprovalSteps => Set<WorkflowApprovalStep>();
    public DbSet<WorkflowChainRule> WorkflowChainRules => Set<WorkflowChainRule>();
    public DbSet<Delegation> Delegations => Set<Delegation>();

    // Timesheet
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public DbSet<TimesheetEntry> TimesheetEntries => Set<TimesheetEntry>();

    // Payroll & Compensation
    public DbSet<PayComponent> PayComponents => Set<PayComponent>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryStructureComponent> SalaryStructureComponents => Set<SalaryStructureComponent>();
    public DbSet<EmployeeSalaryAssignment> EmployeeSalaryAssignments => Set<EmployeeSalaryAssignment>();
    public DbSet<EmployeeSalaryComponentValue> EmployeeSalaryComponentValues => Set<EmployeeSalaryComponentValue>();
    public DbSet<StatutorySettings> StatutorySettings => Set<StatutorySettings>();
    public DbSet<PtSlab> PtSlabs => Set<PtSlab>();
    public DbSet<InvestmentDeclaration> InvestmentDeclarations => Set<InvestmentDeclaration>();
    public DbSet<EmployeeTaxRegimeSelection> EmployeeTaxRegimeSelections => Set<EmployeeTaxRegimeSelection>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollRunItem> PayrollRunItems => Set<PayrollRunItem>();
    public DbSet<PayrollRunItemLine> PayrollRunItemLines => Set<PayrollRunItemLine>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<FullFinalSettlement> FullFinalSettlements => Set<FullFinalSettlement>();

    // Performance / OKR
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<OkrCycle> OkrCycles => Set<OkrCycle>();
    public DbSet<Objective> Objectives => Set<Objective>();
    public DbSet<KeyResult> KeyResults => Set<KeyResult>();
    public DbSet<FeedbackNote> FeedbackNotes => Set<FeedbackNote>();

    // Notifications & Auditing
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    // Engagement / Extras
    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<HelpdeskTicket> HelpdeskTickets => Set<HelpdeskTicket>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<DashboardLayout> DashboardLayouts => Set<DashboardLayout>();

    // Integrations (API keys + webhooks)
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Safety net: guarantees the tenant filter on every ITenantOwned entity,
        // even ones an explicit configuration class didn't set one for.
        TenantQueryFilterApplier.ApplyToAllTenantOwnedEntities(modelBuilder, _tenantContext);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAtUtc = now;
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAtUtc = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
