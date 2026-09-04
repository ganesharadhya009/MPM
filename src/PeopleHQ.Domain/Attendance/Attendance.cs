using PeopleHQ.Domain.Common;

namespace PeopleHQ.Domain.Attendance;

public enum AttendanceSource { Web, Mobile, Biometric }
public enum AttendanceStatus { Present, Absent, HalfDay, OnLeave }
public enum RegularizationStatus { Pending, Approved, Rejected }

public class Shift : TenantOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int GraceMinutes { get; set; }
    public int BreakMinutes { get; set; }
}

public class ShiftAssignment : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public Guid ShiftId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
}

public class AttendanceRecord : TenantOwnedEntity
{
    public Guid EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime? CheckInAtUtc { get; set; }
    public DateTime? CheckOutAtUtc { get; set; }
    public double? CheckInLat { get; set; }
    public double? CheckInLng { get; set; }
    public AttendanceSource Source { get; set; } = AttendanceSource.Web;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    /// <summary>Computed: hours beyond the assigned shift length (FR-ATT-08), tenant-configurable multiplier applied in Application layer.</summary>
    public decimal OvertimeHours { get; set; }
}

public class AttendanceRegularizationRequest : TenantOwnedEntity
{
    public Guid AttendanceRecordId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateTime? RequestedCheckInAtUtc { get; set; }
    public DateTime? RequestedCheckOutAtUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RegularizationStatus Status { get; set; } = RegularizationStatus.Pending;
    public Guid? WorkflowRequestId { get; set; }
}
