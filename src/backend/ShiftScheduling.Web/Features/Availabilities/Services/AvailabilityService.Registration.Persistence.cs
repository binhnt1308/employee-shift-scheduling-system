using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Availabilities.Services;

// Nhóm thao tác chuẩn bị và lưu thay đổi cho nghiệp vụ đăng ký lịch rảnh; validation được thực hiện trước khi vào đây.
public sealed partial class AvailabilityService
{
    private async Task SaveEmployeeAvailabilityChangesAsync(
        int accountId,
        Employee employee,
        SchedulePeriod period,
        List<(DateOnly WorkDate, int ShiftTypeId)> parsedShifts,
        Dictionary<(DateOnly WorkDate, int ShiftTypeId), AvailabilityPreferenceLevel> preferenceMap,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        await _availabilityRepository.SaveEmployeeAvailabilitiesAsync(
            employee.EmployeeId,
            period.SchedulePeriodId,
            parsedShifts,
            preferenceMap,
            now,
            cancellationToken);

        AddAvailabilityAuditLog(
            accountId,
            employee,
            period,
            parsedShifts.Count,
            now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Employee availability saved. EmployeeCode: {EmployeeCode}; PeriodId: {PeriodId}; Count: {Count}",
            employee.EmployeeCode,
            period.SchedulePeriodId,
            parsedShifts.Count);
    }

    private void AddAvailabilityAuditLog(
        int accountId,
        Employee employee,
        SchedulePeriod period,
        int selectedShiftCount,
        DateTime now)
    {
        _auditLogRepository.Add(AuditLogFactory.Create(
            accountId,
            "SAVE_EMPLOYEE_AVAILABILITY",
            nameof(Availability),
            $"{employee.EmployeeCode}_{period.SchedulePeriodId}",
            null,
            new
            {
                schedulePeriodId = period.SchedulePeriodId,
                Count = selectedShiftCount
            },
            now));
    }
}
