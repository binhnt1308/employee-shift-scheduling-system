using Microsoft.Extensions.Logging;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Availabilities.Repositories;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.SchedulePeriods.Repositories;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Infrastructure.Persistence;

namespace ShiftScheduling.Features.Availabilities.Services;

/// <summary>
/// Service quản lý lịch rảnh nhân viên (UC04/BR03/BR04/BR05) và mẫu lịch rảnh tuần (NC-02).
/// Chia thành partial files theo trách nhiệm để tuân thủ giới hạn AR-04 [B] (≤400 dòng/file).
/// </summary>
public sealed partial class AvailabilityService : IAvailabilityService
{
    private readonly IAvailabilityRepository _availabilityRepository;
    private readonly ISchedulePeriodRepository _periodRepository;
    private readonly IShiftTypeRepository _shiftTypeRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IWeeklyAvailabilityPatternRepository _weeklyPatternRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<AvailabilityService> _logger;

    public AvailabilityService(
        IAvailabilityRepository availabilityRepository,
        ISchedulePeriodRepository periodRepository,
        IShiftTypeRepository shiftTypeRepository,
        IEmployeeRepository employeeRepository,
        IAuditLogRepository auditLogRepository,
        IWeeklyAvailabilityPatternRepository weeklyPatternRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AvailabilityService> logger)
    {
        _availabilityRepository = availabilityRepository;
        _periodRepository = periodRepository;
        _shiftTypeRepository = shiftTypeRepository;
        _employeeRepository = employeeRepository;
        _auditLogRepository = auditLogRepository;
        _weeklyPatternRepository = weeklyPatternRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }
}
