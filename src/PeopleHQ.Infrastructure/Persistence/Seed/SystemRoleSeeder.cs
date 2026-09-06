using Microsoft.EntityFrameworkCore;
using PeopleHQ.Domain.Identity;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Persistence.Seed;

/// <summary>Seeds the 4 system roles (00-overview.md §5) with their permission grants for a newly-created tenant.</summary>
public static class SystemRoleSeeder
{
    private static readonly IReadOnlyDictionary<string, string[]> RolePermissionMap = new Dictionary<string, string[]>
    {
        ["TenantAdmin"] = new[]
        {
            Permissions.LocationRead, Permissions.LocationWrite,
            Permissions.DepartmentRead, Permissions.DepartmentWrite,
            Permissions.DesignationRead, Permissions.DesignationWrite,
            Permissions.OrgChartRead,
            Permissions.EmployeeRead, Permissions.EmployeeWrite, Permissions.EmployeeDocumentWrite,
            Permissions.UserInvite, Permissions.UserManage, Permissions.RoleManage,
            Permissions.CandidateRead, Permissions.CandidateWrite,
            Permissions.OnboardingTemplateRead, Permissions.OnboardingTemplateWrite,
            Permissions.OnboardingTaskRead, Permissions.OnboardingTaskWrite,
            Permissions.AttendanceRead, Permissions.ShiftWrite, Permissions.RegularizationApprove,
            Permissions.LeaveTypeWrite, Permissions.LeavePolicyWrite, Permissions.LeaveApprove, Permissions.LeaveRead,
            Permissions.ProjectWrite, Permissions.TimesheetApprove, Permissions.TimesheetRead,
            Permissions.PayComponentWrite, Permissions.SalaryStructureWrite, Permissions.SalaryAssignmentWrite, Permissions.SalaryAssignmentRead,
            Permissions.StatutorySettingsWrite, Permissions.InvestmentDeclarationVerify, Permissions.PayrollRunWrite, Permissions.PayrollRunApprove, Permissions.PayslipRead,
            Permissions.GoalWrite, Permissions.OkrWrite,
            Permissions.WorkflowApprove, Permissions.WorkflowChainRuleWrite,
            Permissions.ReportRead, Permissions.AuditLogRead,
            Permissions.SurveyWrite, Permissions.AssetWrite, Permissions.HelpdeskTicketWrite, Permissions.HelpdeskTicketManage, Permissions.AnnouncementWrite, Permissions.AnnouncementRead,
            Permissions.CustomFieldDefinitionWrite, Permissions.CustomFieldValueWrite, Permissions.BulkImportWrite,
            Permissions.OkrCycleWrite,
            Permissions.DashboardRead, Permissions.DashboardWrite,
            Permissions.ApiKeyWrite, Permissions.WebhookWrite,
            Permissions.SsoConfigWrite,
            Permissions.BillingRead, Permissions.BillingWrite,
        },
        ["Manager"] = new[]
        {
            Permissions.EmployeeRead, Permissions.OrgChartRead,
            Permissions.AttendanceCheckInOut, Permissions.AttendanceRead, Permissions.RegularizationApprove,
            Permissions.LeaveApply, Permissions.LeaveApprove, Permissions.LeaveRead,
            Permissions.TimesheetWrite, Permissions.TimesheetApprove, Permissions.TimesheetRead,
            Permissions.GoalWrite, Permissions.OkrWrite, Permissions.FeedbackWrite,
            Permissions.WorkflowApprove, Permissions.DelegationWrite,
            Permissions.NotificationRead, Permissions.PayslipReadOwn,
            Permissions.HrProcessRequestWrite,
            Permissions.SurveyRespond, Permissions.HelpdeskTicketWrite, Permissions.AnnouncementRead,
            Permissions.DashboardRead,
        },
        ["Employee"] = new[]
        {
            Permissions.OrgChartRead,
            Permissions.AttendanceCheckInOut,
            Permissions.LeaveApply, Permissions.LeaveRead,
            Permissions.TimesheetWrite,
            Permissions.InvestmentDeclarationWrite, Permissions.PayslipReadOwn,
            Permissions.GoalWrite, Permissions.OkrWrite, Permissions.FeedbackWrite,
            Permissions.NotificationRead,
            Permissions.HrProcessRequestWrite,
            Permissions.SurveyRespond, Permissions.HelpdeskTicketWrite, Permissions.AnnouncementRead,
            Permissions.DashboardRead,
        },
        ["Recruiter"] = new[]
        {
            Permissions.EmployeeRead, Permissions.CandidateRead, Permissions.CandidateWrite,
            Permissions.OnboardingTemplateRead, Permissions.OnboardingTaskRead, Permissions.OnboardingTaskWrite,
            Permissions.AnnouncementRead,
            Permissions.DashboardRead,
        },
    };

    public static async Task SeedForTenantAsync(AppDbContext db, Guid tenantId, CancellationToken ct = default)
    {
        var allKeys = RolePermissionMap.Values.SelectMany(p => p).Distinct();
        var permissionsByKey = new Dictionary<string, Permission>();

        foreach (var key in allKeys)
        {
            var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Key == key, ct);
            if (permission is null)
            {
                permission = new Permission { Key = key, Description = key };
                db.Permissions.Add(permission);
            }
            permissionsByKey[key] = permission;
        }

        foreach (var (roleName, permissionKeys) in RolePermissionMap)
        {
            var role = new Role { TenantId = tenantId, Name = roleName, IsSystem = true };
            db.Roles.Add(role);
            foreach (var key in permissionKeys)
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionsByKey[key].Id });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
