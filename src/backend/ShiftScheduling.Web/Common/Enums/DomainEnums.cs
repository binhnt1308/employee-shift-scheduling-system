namespace ShiftScheduling.Models.Enums;

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    Employee = 3
}

public enum EmployeePosition
{
    Sales = 1,
    Cashier = 2
}

public enum EmploymentType
{
    PartTime = 1,
    FullTime = 2
}

public enum EmployeeLevel
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3
}

public enum RegistrationStatus
{
    Preparing = 1,
    Open = 2,
    Closed = 3
}

public enum ScheduleStatus
{
    Proposed = 1,
    Draft = 2,
    Published = 3
}

public enum AssignmentSource
{
    Manual = 1,
    ORTools = 2,
    ShiftTransfer = 3
}

public enum LeaveRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum ReceiverStatus
{
    Pending = 1,
    Accepted = 2,
    Rejected = 3
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum NotificationType
{
    SchedulePublished = 1,
    ScheduleChanged = 2,
    LeaveRequestResult = 3,
    ShiftTransferRequest = 4,
    ShiftTransferResult = 5,
    RegistrationOpened = 6,
    LeaveRequestSubmitted = 7
}

/// <summary>
/// Mức nguyện vọng của nhân viên đối với ca rảnh (NC-02).
/// Normal: có thể làm. Preferred: mong muốn được xếp.
/// Không có mức Avoid – ô không chọn đã mang nghĩa không rảnh.
/// </summary>
public enum AvailabilityPreferenceLevel
{
    Normal = 1,
    Preferred = 2
}
