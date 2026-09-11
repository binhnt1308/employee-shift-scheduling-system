# Employee Shift Scheduling System

## 1. Giới thiệu

**Tên tiếng Việt:** Hệ thống lập lịch và quản lý phân công ca làm việc cho nhân viên  
**Tên tiếng Anh:** Employee Shift Scheduling System

Hệ thống web hỗ trợ quản lý nhân viên và lập lịch ca làm việc cho cửa hàng bán lẻ quy mô nhỏ.

Các chức năng chính gồm quản lý tài khoản và nhân viên, quản lý kỳ xếp lịch, đăng ký lịch rảnh, phân công ca thủ công, sinh lịch bằng Google OR-Tools CP-SAT, công bố lịch, xin nghỉ, đổi ca, điều chỉnh giờ làm thực tế, thông báo và báo cáo.

---

## 2. Công nghệ sử dụng

- **Backend:** ASP.NET Core MVC
- **Target framework:** .NET 8 (`net8.0`)
- **Ngôn ngữ:** C#
- **Frontend:** Razor View, Bootstrap 5, HTML5, CSS3, JavaScript
- **ORM:** Entity Framework Core 8
- **Database:** SQL Server
- **Authentication:** Cookie Authentication
- **Password hashing:** PBKDF2
- **Scheduling engine:** Google OR-Tools CP-SAT
- **Testing:** xUnit
- **CI:** GitHub Actions
- **Version control:** Git, GitHub

---

## 3. Chức năng hiện tại

| Chức năng | Trạng thái |
| --- | --- |
| Đăng nhập / đăng xuất | ✅ Hoàn thành |
| Quản lý tài khoản và phân quyền | ✅ Hoàn thành |
| Quản lý nhân viên | ✅ Hoàn thành |
| Quản lý kỳ xếp lịch | ✅ Hoàn thành |
| Cấu hình định mức nhân sự | ✅ Hoàn thành |
| Đăng ký lịch rảnh | ✅ Hoàn thành |
| Lưu mẫu lịch rảnh theo tuần | ✅ Hoàn thành |
| Mức ưu tiên Normal / Preferred | ✅ Hoàn thành |
| Phân công ca thủ công | ✅ Hoàn thành |
| Sinh lịch bằng Google OR-Tools | ✅ Hoàn thành |
| Kiểm tra ràng buộc trước khi công bố | ✅ Hoàn thành |
| Lưu lịch Draft | ✅ Hoàn thành |
| Công bố lịch | ✅ Hoàn thành |
| Nhân viên xem lịch cá nhân | ✅ Hoàn thành |
| Quản lý đơn xin nghỉ | ✅ Hoàn thành |
| Quản lý yêu cầu đổi ca | ✅ Hoàn thành |
| Điều chỉnh giờ làm thực tế sau khi ca kết thúc | ✅ Hoàn thành |
| Báo cáo số ca, số ngày và giờ làm | ✅ Hoàn thành |
| Xuất báo cáo CSV | ✅ Hoàn thành |
| Thông báo trong hệ thống | ✅ Hoàn thành |
| Email notification | ✅ Có hỗ trợ, cần cấu hình SMTP để gửi email thật |

---

## 4. Vai trò

Hệ thống sử dụng ba vai trò:

- Admin
- Manager
- Employee

| Chức năng | Admin | Manager | Employee |
| --- | :---: | :---: | :---: |
| Quản lý tài khoản | ✅ | ❌ | ❌ |
| Quản lý nhân viên | ✅ | ✅ | ❌ |
| Quản lý kỳ lịch | ❌ | ✅ | ❌ |
| Cấu hình định mức nhân sự | ❌ | ✅ | ❌ |
| Đăng ký lịch rảnh | ❌ | ❌ | ✅ |
| Xem tổng hợp lịch rảnh | ❌ | ✅ | ❌ |
| Phân công ca | ❌ | ✅ | ❌ |
| Sinh lịch bằng OR-Tools | ❌ | ✅ | ❌ |
| Công bố lịch | ❌ | ✅ | ❌ |
| Xem lịch cá nhân | ❌ | ❌ | ✅ |
| Gửi đơn xin nghỉ | ❌ | ❌ | ✅ |
| Duyệt / từ chối đơn xin nghỉ | ❌ | ✅ | ❌ |
| Gửi / phản hồi yêu cầu đổi ca | ❌ | ❌ | ✅ |
| Duyệt yêu cầu đổi ca | ❌ | ✅ | ❌ |
| Điều chỉnh giờ làm thực tế | ❌ | ✅ | ❌ |
| Báo cáo tổng hợp | ❌ | ✅ | ❌ |
| Báo cáo cá nhân | ❌ | ❌ | ✅ |

---

## 5. Quy trình lập lịch

```text
Manager tạo kỳ lịch
        ↓
Cấu hình định mức nhân sự
        ↓
Mở đăng ký lịch rảnh
        ↓
Employee đăng ký lịch rảnh
        ↓
Manager đóng đăng ký
        ↓
Phân công thủ công
        hoặc
Google OR-Tools CP-SAT
        ↓
Sinh lịch đề xuất
        ↓
Manager lựa chọn / chỉnh sửa
        ↓
Draft
        ↓
Kiểm tra ràng buộc
        ↓
Publish
        ↓
Employee xem lịch
        ↓
Báo cáo / điều chỉnh giờ thực tế
```

Google OR-Tools chỉ sinh **lịch đề xuất**. Manager phải chủ động sử dụng đề xuất và công bố lịch.

---

# Chạy project

## Yêu cầu môi trường

- Windows 10/11
- .NET 8 SDK
- SQL Server Express LocalDB
- PowerShell

Sau khi đã tải source code về máy, mở PowerShell tại thư mục gốc của project và chạy lần lượt:

### Bước 1. Restore package

```powershell
dotnet restore src/backend/ShiftScheduling.sln
```

### Bước 2. Khởi động SQL Server LocalDB

```powershell
sqllocaldb start MSSQLLocalDB
```

### Bước 3. Cấu hình database demo

```powershell
$env:ConnectionStrings__DefaultConnection="Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDemo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"; $env:DemoData__Enabled="true"
```

### Bước 4. Chạy ứng dụng

```powershell
dotnet run --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj --configuration Release
```

Sau khi ứng dụng khởi động, mở URL được hiển thị trong terminal.

Ứng dụng sẽ tự động:

```text
Kết nối database
→ áp dụng EF Core migration còn thiếu
→ tạo dữ liệu demo nếu database mới
→ khởi động website
```

Không cần tạo database hoặc seed dữ liệu thủ công.

---

## Tài khoản demo

### Manager

```text
Username: quanly
Password: quanly123
```

### Employee

```text
Username: nv02
Password: Password123!
Employee: NV02 - Chiêm Trung Hậu
```

### Admin

```text
Username: admin
Password: admin123
```

> Tài khoản legacy `nhanvien` không còn được sử dụng trên fresh database.

---

## Dữ liệu demo

Khi:

```text
DemoData__Enabled=true
```

và database chưa có dữ liệu nghiệp vụ, hệ thống tự tạo dữ liệu phục vụ demo.

### Nhân viên

Fresh database có 8 nhân viên:

```text
NV01
NV02
NV03
NV04
NV05
NV06
NV07
NV08
```

Cơ cấu:

```text
4 Sales
4 Cashier
5 FullTime
3 PartTime
```

Nhân viên được dùng cho luồng demo chính:

```text
NV02 - Chiêm Trung Hậu
```

### Ca làm việc

Hệ thống sử dụng 3 ca cố định:

```text
MORNING     08:00 - 12:00
AFTERNOON   12:00 - 16:00
EVENING     16:00 - 20:00
```

### Kịch bản demo

Seeder tạo hai kỳ chính.

#### Kỳ đang mở đăng ký

Dữ liệu gồm:

- Kỳ lịch trạng thái Open.
- 7 ngày × 3 ca = 21 tổ hợp ngày-ca.
- Staffing Requirement.
- Availability.
- Mức ưu tiên `Normal`.
- Mức ưu tiên `Preferred`.

Có thể sử dụng để demo:

```text
Manager xem lịch rảnh
→ sinh lịch bằng OR-Tools
→ xem lịch đề xuất
```

#### Kỳ đã công bố

Dữ liệu gồm:

- WorkSchedule trạng thái Published.
- ShiftAssignment.
- Lịch làm việc cá nhân.
- Dữ liệu báo cáo.
- Dữ liệu điều chỉnh giờ làm thực tế.

Ví dụ dữ liệu điều chỉnh giờ làm:

```text
Employee: NV02 - Chiêm Trung Hậu
Shift: EVENING
Scheduled hours: 4.00
Actual worked hours: 3.50
Reason: về sớm 30 phút theo điều chỉnh của quản lý
```

Seeder được thiết kế để:

- Không tạo dữ liệu trùng khi chạy lại trên database demo hoàn chỉnh.
- Không tự thêm dữ liệu demo vào database nghiệp vụ đã có dữ liệu.
- Dừng và báo lỗi nếu phát hiện dataset demo bị thiếu một phần.

---

## Migration

Schema database được quản lý bằng **Entity Framework Core Migrations**.

Ứng dụng tự động áp dụng migration còn thiếu khi khởi động.

Trong trường hợp cần chạy migration thủ công:

```powershell
dotnet ef database update `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj `
  --startup-project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj
```

Nếu máy chưa có công cụ `dotnet ef`, cài bằng:

```powershell
dotnet tool install --global dotnet-ef --version "8.*"
```

Sau đó chạy lại lệnh migration phía trên.

> Không chỉnh sửa schema database trực tiếp bằng SSMS thay cho EF Core Migration.

---

## Cấu hình

### Connection string

Biến môi trường:

```text
ConnectionStrings__DefaultConnection
```

Ví dụ:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDemo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

### Demo data

Biến môi trường:

```text
DemoData__Enabled
```

Bật:

```powershell
$env:DemoData__Enabled="true"
```

Tắt:

```powershell
$env:DemoData__Enabled="false"
```

### Email / SMTP

Thông tin SMTP thật không được commit vào repository.

Cấu hình SMTP bằng Environment Variables hoặc User Secrets dựa trên file mẫu:

```text
src/backend/ShiftScheduling.Web/appsettings.Example.json
```

Không commit:

- SMTP password.
- App Password.
- API key.
- Connection string production.
- Credential thật.

SMTP không bắt buộc để chạy các chức năng demo chính.

---

## User Secrets

Nếu muốn lưu cấu hình local thay vì khai báo lại Environment Variable mỗi lần mở PowerShell, có thể sử dụng User Secrets.

### Connection string

```powershell
dotnet user-secrets set `
  "ConnectionStrings:DefaultConnection" `
  "Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDemo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj
```

### Demo data

```powershell
dotnet user-secrets set `
  "DemoData:Enabled" `
  "true" `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj
```

User Secrets nằm ngoài repository và không được commit vào Git.

---

## Lỗi kết nối database thường gặp

### Không tìm thấy `MSSQLLocalDB`

Kiểm tra LocalDB:

```powershell
sqllocaldb info MSSQLLocalDB
```

Nếu instance tồn tại nhưng chưa chạy:

```powershell
sqllocaldb start MSSQLLocalDB
```

Nếu không có instance `MSSQLLocalDB`, cần cài SQL Server Express LocalDB trước khi chạy project.

### Không kết nối được database

Kiểm tra lại biến:

```text
ConnectionStrings__DefaultConnection
```

Có thể thiết lập lại:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDemo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

### Seeder báo `Partial demo dataset detected`

Database đang chứa một phần dataset demo nhưng không còn đầy đủ.

Không nên sửa từng record bằng tay.

Có thể sử dụng một database demo mới:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDemo2;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Sau đó chạy lại ứng dụng.

---

## 6. Kiến trúc backend

Backend được tổ chức chủ yếu theo Feature.

Luồng xử lý chính:

```text
Controller
    ↓
Service
    ↓
Repository
    ↓
Entity Framework Core
    ↓
SQL Server
```

Business logic được xử lý tại tầng Service.

Controller không truy cập trực tiếp `DbContext`.

Các feature chính:

```text
Features/
├── Accounts/
├── Authentication/
├── Availabilities/
├── Dashboard/
├── Employees/
├── LeaveRequests/
├── Reports/
├── SchedulePeriods/
├── ShiftTransfers/
├── ShiftTypes/
└── WorkSchedules/
```

---

## 7. Một số quy tắc nghiệp vụ chính

- Một kỳ lịch kéo dài 7 ngày.
- Hệ thống sử dụng 3 ca cố định.
- Mỗi kỳ có 21 tổ hợp ngày-ca.
- Không cho phép tạo trùng kỳ cùng tuần.
- Employee chỉ đăng ký lịch rảnh khi kỳ đang mở và còn hạn đăng ký.
- Chỉ phân công nhân viên đang hoạt động.
- Nhân viên phải đăng ký rảnh trước khi được phân công.
- Nhân viên có đơn nghỉ được duyệt không được phân vào ca nghỉ tương ứng.
- Phân công phải đúng vị trí công việc.
- PartTime tối đa 1 ca/ngày.
- FullTime tối đa 6 ngày/tuần.
- Mỗi ca phải đáp ứng định mức nhân sự.
- Mỗi ca phải đáp ứng yêu cầu kinh nghiệm.
- Draft có thể lưu khi chưa hoàn chỉnh.
- Không Publish khi còn lỗi cứng.
- OR-Tools chỉ tạo proposal.
- Trước khi Publish, hệ thống kiểm tra lại dữ liệu mới nhất.
- `Preferred` được ưu tiên hơn `Normal` trong mục tiêu mềm.
- Hệ thống hạn chế ca tối ở hai ngày liên tiếp khi có thể.
- Đổi ca là chuyển ca một chiều.
- Manager chỉ được điều chỉnh giờ làm thực tế sau khi ca kết thúc.
- Giờ làm thực tế điều chỉnh phải nằm trong giới hạn hợp lệ của ca.
- Điều chỉnh khác giá trị hệ thống phải có lý do.

---

## 8. Chạy kiểm thử

Project sử dụng xUnit.

### Bộ test chạy trong CI

```powershell
dotnet test src/backend/ShiftScheduling.sln `
  --configuration Release `
  --filter "FullyQualifiedName!~ControllerDatabaseIntegrationTests"
```

Kết quả kiểm tra gần nhất trên branch `tuan-10`:

```text
Build:
0 warnings
0 errors

DemoDataSeeder:
29 passed
0 failed

Non-integration:
456 passed
0 failed

CI-equivalent:
463 passed
0 failed
```

GitHub Actions trên branch `tuan-10` hiện đang xanh.

---

## 9. Integration test với SQL Server

Các Controller–Service–Repository–Database integration test sử dụng database test riêng.

Không sử dụng database demo để chạy integration test.

Integration test yêu cầu biến môi trường:

```text
SHIFT_SCHEDULING_TEST_CONNECTION
```

Ví dụ:

```powershell
$env:SHIFT_SCHEDULING_TEST_CONNECTION="Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDb_IntegrationTest;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Sau đó chạy:

```powershell
dotnet test src/backend/ShiftScheduling.sln --configuration Release
```

Nếu chưa thiết lập `SHIFT_SCHEDULING_TEST_CONNECTION`, nhóm integration test sẽ từ chối chạy để tránh thao tác nhầm database demo hoặc database đang sử dụng.

---

## 10. Tài liệu kiểm thử

Tài liệu kiểm thử được lưu tại:

```text
docs/kiem-thu/
```

Các file chính:

```text
testcase.md
matrantruyvet.md
BR_Test Coverage.md
```

Nội dung gồm:

- Test case cho các chức năng chính.
- Test case cho Business Rule.
- Ma trận truy vết yêu cầu → test case.
- Coverage theo Business Rule.
- Kết quả kiểm thử.

Các kỹ thuật kiểm thử được sử dụng:

- Phân hoạch tương đương.
- Phân tích giá trị biên.
- Bảng quyết định.
- Chuyển trạng thái.
- Kiểm thử hồi quy.

---

## 11. CI

Repository sử dụng GitHub Actions.

Workflow chạy khi push hoặc mở Pull Request.

Luồng CI:

```text
Restore
→ Format check
→ Build
→ Test
```

Các Controller Database Integration Test cần SQL Server riêng được tách khỏi suite chạy trên runner `ubuntu-latest`.

Branch chỉ nên merge khi CI xanh.

---

## 12. Trạng thái hiện tại

Branch phát triển hiện tại:

```text
tuan-10
```

Các phần chính hiện đã có:

```text
Weekly Availability Pattern
Normal / Preferred
Manager Availability
Manual Scheduling
OR-Tools Proposal
Publish Validation
Personal Schedule
Leave Request
Shift Transfer
NC-07 Workday Adjustment
Internal Notification
Email Workflow
Manager Report
Personal Report
CSV Export
Fresh Database Demo Seed
```

Fresh database đã được kiểm tra theo luồng:

```text
Migration
→ Demo Seed
→ Manager Availability
→ OR-Tools
→ Published Schedule
→ Report
→ NC-07
→ Employee Schedule
```

---

## 13. Repository

**GitHub:**  
https://github.com/binhnt1308/employee-shift-scheduling-system

**Branch phát triển hiện tại:** `tuan-10`

---

## 14. Thông tin sinh viên

**Sinh viên thực hiện:** Nguyễn Thanh Bình  
**MSSV:** 2354050017  
**Giảng viên hướng dẫn:** Th.S Võ Việt Khoa