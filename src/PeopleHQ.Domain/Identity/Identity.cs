using Microsoft.AspNetCore.Identity;
using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Identity;

public enum UserStatus { Invited, Active, Disabled }

/// <summary>Extends ASP.NET Core Identity for password/login machinery only — Roles/Permissions below are custom (00-overview.md §5), not IdentityRole.</summary>
public class AppUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Invited;
    public bool MfaEnabled { get; set; }
    public string? MfaSecretEncrypted { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Tenant-scoped role. System roles (TenantAdmin/Manager/Employee/Recruiter/Auditor) are cloned per tenant on creation.</summary>
public class Role : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}

/// <summary>Global — not tenant-owned. e.g. "employee.write", "leave.approve".</summary>
public class Permission : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByToken { get; set; }
    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}

/// <summary>String constants — every [RequirePermission(...)] references one of these, never a raw literal.</summary>
public static class Permissions
{
    // Org structure
    public const string LocationRead = "location.read";
    public const string LocationWrite = "location.write";
    public const string DepartmentRead = "department.read";
    public const string DepartmentWrite = "department.write";
    public const string DesignationRead = "designation.read";
    public const string DesignationWrite = "designation.write";
    public const string OrgChartRead = "orgchart.read";

    // Employees
    public const string EmployeeRead = "employee.read";
    public const string EmployeeWrite = "employee.write";
    public const string EmployeeDocumentWrite = "employee.document.write";

    // Users/roles
    public const string UserInvite = "user.invite";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";

    // Onboarding
    public const string CandidateRead = "candidate.read";
    public const string CandidateWrite = "candidate.write";
    public const string OnboardingTemplateRead = "onboarding.template.read";
    public const string OnboardingTemplateWrite = "onboarding.template.write";
    public const string OnboardingTaskRead = "onboarding.task.read";
    public const string OnboardingTaskWrite = "onboarding.task.write";

    // Attendance
    public const string AttendanceCheckInOut = "attendance.checkinout";
    public const string AttendanceRead = "attendance.read";
    public const string ShiftWrite = "shift.write";
    public const string RegularizationWrite = "regularization.write";
    public const string RegularizationApprove = "regularization.approve";

    // Leave
    public const string LeaveTypeWrite = "leavetype.write";
    public const string LeavePolicyWrite = "leavepolicy.write";
    public const string LeaveApply = "leave.apply";
    public const string LeaveApprove = "leave.approve";
    public const string LeaveRead = "leave.read";

    // Timesheet
    public const string ProjectWrite = "project.write";
    public const string TimesheetWrite = "timesheet.write";
    public const string TimesheetApprove = "timesheet.approve";
    public const string TimesheetRead = "timesheet.read";

    // Payroll
    public const string PayComponentWrite = "paycomponent.write";
    public const string SalaryStructureWrite = "salarystructure.write";
    public const string SalaryAssignmentWrite = "salaryassignment.write";
    public const string SalaryAssignmentRead = "salaryassignment.read";
    public const string StatutorySettingsWrite = "statutorysettings.write";
    public const string InvestmentDeclarationWrite = "investmentdeclaration.write";
    public const string InvestmentDeclarationVerify = "investmentdeclaration.verify";
    public const string PayrollRunWrite = "payrollrun.write";
    public const string PayrollRunApprove = "payrollrun.approve";
    public const string PayslipRead = "payslip.read";
    public const string PayslipReadOwn = "payslip.read.own";

    // Performance
    public const string GoalWrite = "goal.write";
    public const string OkrWrite = "okr.write";
    public const string FeedbackWrite = "feedback.write";

    // Workflow
    public const string WorkflowApprove = "workflow.approve";
    public const string WorkflowChainRuleWrite = "workflowchainrule.write";
    public const string DelegationWrite = "delegation.write";

    // Notifications / Reports / Admin
    public const string NotificationRead = "notification.read";
    public const string ReportRead = "report.read";
    public const string AuditLogRead = "auditlog.read";

    // Engagement extras
    public const string SurveyWrite = "survey.write";
    public const string SurveyRespond = "survey.respond";
    public const string AssetWrite = "asset.write";
    public const string HelpdeskTicketWrite = "helpdeskticket.write";
    public const string HelpdeskTicketManage = "helpdeskticket.manage";
    public const string AnnouncementWrite = "announcement.write";
    public const string AnnouncementRead = "announcement.read";

    // Phase 2: HR Process requests, Custom Fields, Bulk Import
    public const string HrProcessRequestWrite = "hrprocess.write";
    public const string CustomFieldDefinitionWrite = "customfield.definition.write";
    public const string CustomFieldValueWrite = "customfield.value.write";
    public const string BulkImportWrite = "bulkimport.write";

    // Phase 3: OKR cycle administration (Objective/KeyResult self-service reuses OkrWrite above)
    public const string OkrCycleWrite = "okrcycle.write";

    // Configurable dashboards ("most needed options" #8)
    public const string DashboardRead = "dashboard.read";
    public const string DashboardWrite = "dashboard.write";

    // Phase 4: API keys + webhooks for tenants (05-enhancements-and-roadmap.md)
    public const string ApiKeyWrite = "apikey.write";
    public const string WebhookWrite = "webhook.write";
}
