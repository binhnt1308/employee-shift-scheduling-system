# Ma trận truy vết thực thi (Implementation Traceability Matrix)

| FR | UC | BR | Chức năng | Code | Tests | Status |
|---|---|---|---|---|---|---|
| FR01 | UC01 | BR01 | Quản lý tài khoản & Xác thực | `ShiftScheduling.Web/Features/Accounts`, `ShiftScheduling.Web/Features/Authentication` | `TC01-01` đến `TC01-08` (`ShiftScheduling.Tests/Accounts`, `Authentication`, `Common`) | DONE |
| FR02 | UC02 | BR02 | Quản lý nhân viên | `ShiftScheduling.Web/Features/Employees` | `TC02-01` đến `TC02-04` (`ShiftScheduling.Tests/Employees`) | DONE |
| FR03 | UC03 | BR03, BR04, BR12, BR13 | Thiết lập kỳ lịch và định mức ca | `ShiftScheduling.Web/Features/SchedulePeriods`, `ShiftScheduling.Web/Features/ShiftTypes` | `TC03-01` đến `TC03-18` (`ShiftScheduling.Tests/ShiftTypes`, `SchedulePeriods`) | DONE |
| FR04 | UC04 | BR03, BR04, BR05 | Quản lý lịch rảnh | - | - | NOT STARTED |
| FR05 | UC05 | BR05, BR06, BR07, BR08, BR09, BR10, BR11, BR12, BR13, BR14, BR16, BR18, BR19 | Phân công và điều chỉnh lịch làm việc | - | - | NOT STARTED |
| FR06 | UC06 | BR15 | Sinh lịch đề xuất tự động (OR-Tools) | - | - | NOT STARTED |
| FR07 | UC07 | BR14, BR16 | Duyệt và công bố lịch | - | - | NOT STARTED |
| FR08 | UC08 | BR01 | Xem lịch đã công bố | - | - | NOT STARTED |
| FR09 | UC09 | BR06 | Quản lý đơn xin nghỉ | - | - | NOT STARTED |
| FR10 | UC10 | BR17 | Quản lý yêu cầu đổi ca | - | - | NOT STARTED |
| FR11 | UC11 | BR01 | Tổng hợp số ca và giờ làm | - | - | NOT STARTED |
