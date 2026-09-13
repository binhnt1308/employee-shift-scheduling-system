using System.Text;
using ShiftScheduling.Features.Reports.ViewModels;
using ShiftScheduling.Models.Entities;
using ShiftScheduling.Models.Enums;

namespace ShiftScheduling.Features.Reports.Services;

public sealed partial class WorkReportService
{
    /// <inheritdoc />
    public async Task<EmployeeWorkReportOverviewViewModel?> GetEmployeeOwnReportAsync(
        int accountId,
        int schedulePeriodId,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByAccountIdAsync(accountId, true, cancellationToken);
        if (employee is null)
        {
            return null;
        }

        var publishedPeriods = await GetPublishedPeriodOptionsAsync(cancellationToken);
        if (!publishedPeriods.Any())
        {
            return new EmployeeWorkReportOverviewViewModel
            {
                PeriodOptions = new List<ReportPeriodOptionViewModel>()
            };
        }

        schedulePeriodId = ResolveSchedulePeriodId(schedulePeriodId, publishedPeriods);

        var period = await _periodRepository.GetByIdAsync(schedulePeriodId, true, false, cancellationToken);
        if (period is null)
        {
            return null;
        }

        var publishedSchedule = await _workScheduleRepository.GetPublishedByPeriodIdAsync(
            schedulePeriodId, cancellationToken);

        var approvedLeaves = await _leaveRequestRepository.GetApprovedLeavesByPeriodAsync(
            schedulePeriodId, cancellationToken);
        var leaveLookup = new ApprovedLeaveLookup(approvedLeaves, schedulePeriodId);

        var rows = BuildReportRows(
            new List<Employee> { employee },
            publishedSchedule?.ShiftAssignments.ToList() ?? new List<ShiftAssignment>(),
            schedulePeriodId,
            leaveLookup,
            employee.EmployeeId,
            null,
            null);

        return new EmployeeWorkReportOverviewViewModel
        {
            SchedulePeriodId = period.SchedulePeriodId,
            PeriodName = period.PeriodName,
            WeekStartDate = period.WeekStartDate,
            WeekEndDate = period.WeekEndDate,
            PeriodOptions = publishedPeriods,
            Summary = rows.FirstOrDefault()
        };
    }

    /// <inheritdoc />
    public async Task<byte[]> GenerateReportCsvAsync(
        int schedulePeriodId,
        int? employeeIdFilter = null,
        EmployeePosition? positionFilter = null,
        EmploymentType? employmentTypeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var overview = await GetManagerReportOverviewAsync(
            schedulePeriodId, employeeIdFilter, positionFilter, employmentTypeFilter, cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("STT,Mã NV,Họ tên,Vị trí,Hình thức,Số ca,Số ngày,Tổng giờ,Cảnh báo / Mức dự kiến");

        if (overview is not null)
        {
            foreach (var row in overview.Rows)
            {
                var posStr = row.Position == EmployeePosition.Sales ? "Tư vấn" : "Thu ngân";
                var typeStr = row.EmploymentType == EmploymentType.PartTime ? "PT" : "FT";
                var code = EscapeCsv(row.EmployeeCode);
                var name = EscapeCsv(row.FullName);
                var hoursStr = row.TotalHours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                var warn = EscapeCsv(row.Warning);
                builder.AppendLine(
                    $"{row.Index},{code},{name},{posStr},{typeStr}," +
                    $"{row.TotalShifts},{row.TotalWorkDays},{hoursStr},{warn}");
            }
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static string EscapeCsv(string text)
    {
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }
}
