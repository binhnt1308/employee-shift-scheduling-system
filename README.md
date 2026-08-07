# Employee Shift Scheduling & Workforce Management System

## Tên đề tài

- **Tên tiếng Anh:** Employee Shift Scheduling System
- **Tên tiếng Việt:** Hệ thống lập lịch và quản lý phân công ca làm việc cho nhân viên

## Mô tả ngắn

Hệ thống web hỗ trợ quản lý nhân viên và lập lịch ca làm việc cho cửa hàng bán lẻ quy mô nhỏ
(khoảng 10–20 nhân viên).

Mục tiêu của hệ thống là hỗ trợ quản lý dữ liệu nhân sự, thiết lập ca làm việc, tạo kỳ lịch theo tuần,
cấu hình định mức nhân sự và từng bước tự động hóa quá trình phân ca bằng Google OR-Tools.

## Tiến độ hiện tại

### Đã hoàn thành

- Đăng nhập / đăng xuất bằng Cookie Authentication.
- Hash mật khẩu bằng PBKDF2.
- Quản lý tài khoản.
- Khóa / mở khóa tài khoản.
- Phân quyền người dùng.
- Quản lý nhân viên.
- Quản lý 3 ca làm việc cố định:
  - Ca sáng: `08:00–12:00`
  - Ca chiều: `12:00–16:00`
  - Ca tối: `16:00–20:00`
- Thiết lập kỳ lịch làm việc 7 ngày.
- Tự động tạo `7 ngày × 3 ca = 21` tổ hợp ngày-ca.
- Cấu hình định mức nhân sự theo vị trí Tư vấn / Thu ngân.
- Kiểm tra yêu cầu nhân sự Level 2 trở lên.
- Phân quyền giao diện và backend theo vai trò.
- Tách kiến trúc backend theo Feature.
- Kiểm thử tự động và script kiểm tra project.

### Trạng thái kiểm thử gần nhất

- **Build:** PASS
- **Warnings:** 0
- **Errors:** 0
- **Tests:** 36/36 PASS
- **Project verification:** PASS

### Chức năng tiếp theo

- Đăng ký lịch rảnh.
- Phân công ca thủ công.
- Sinh lịch đề xuất bằng Google OR-Tools.
- Công bố lịch làm việc.
- Quản lý đơn xin nghỉ.
- Quản lý yêu cầu đổi ca.
- Báo cáo tổng hợp giờ/ca làm.
- Docker, CI/CD và triển khai.

## Phân quyền hiện tại

| Chức năng | Admin | Manager | Employee |
|---|:---:|:---:|:---:|
| Tổng quan | ✅ | ✅ | ✅ |
| Quản lý tài khoản | ✅ | ❌ | ❌ |
| Khóa / mở tài khoản | ✅ | ❌ | ❌ |
| Phân quyền | ✅ | ❌ | ❌ |
| Quản lý nhân viên | ✅ | ✅ | ❌ |
| Cấu hình ca làm | ❌ | ✅ | ❌ |
| Thiết lập kỳ lịch / định mức | ❌ | ✅ | ❌ |
| Phân công lịch | ❌ | ✅ | ❌ |

## Công nghệ sử dụng

- **Backend:** ASP.NET Core MVC 8 (.NET 8, C#)
- **Frontend:** Razor View, Bootstrap 5, HTML5, CSS3, JavaScript
- **ORM:** Entity Framework Core 8
- **Database:** SQL Server LocalDB
- **Authentication:** Cookie Authentication
- **Password Hashing:** PBKDF2
- **Testing:** xUnit
- **Version Control:** Git, GitHub
- **Scheduling Engine:** Google OR-Tools *(giai đoạn tiếp theo)*

## Kiến trúc backend

Backend hiện được tổ chức theo Feature:

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

Business logic được xử lý tại tầng Service. Controller không truy cập trực tiếp `DbContext`.

## Cấu trúc Repository

```text
employee-shift-scheduling-system/
├── README.md
├── .gitignore
├── .editorconfig
├── docs/
│   ├── BaoCao/
│   ├── kiem-thu/
│   └── weekly/
└── src/
    ├── backend/
    │   ├── ShiftScheduling.Web/
    │   │   ├── Common/
    │   │   ├── Features/
    │   │   │   ├── Accounts/
    │   │   │   ├── Authentication/
    │   │   │   ├── Dashboard/
    │   │   │   ├── Employees/
    │   │   │   ├── SchedulePeriods/
    │   │   │   └── ShiftTypes/
    │   │   ├── Infrastructure/
    │   │   ├── Views/
    │   │   ├── wwwroot/
    │   │   ├── Program.cs
    │   │   └── ShiftScheduling.Web.csproj
    │   ├── ShiftScheduling.Tests/
    │   ├── ShiftScheduling.sln
    │   └── verify_project.ps1
    └── frontend/
        └── README.md
```

## Yêu cầu hệ thống

- .NET 8 SDK
- SQL Server LocalDB hoặc SQL Server Express
- Visual Studio 2022 / VS Code

## Cài đặt và chạy ứng dụng

Từ thư mục root của repository:

### 1. Restore package

```powershell
dotnet restore src/backend/ShiftScheduling.sln
```

### 2. Build

```powershell
dotnet build src/backend/ShiftScheduling.sln `
  --no-restore `
  --warnaserror
```

### 3. Cập nhật cơ sở dữ liệu

```powershell
dotnet ef database update `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj
```

Nếu máy chưa có EF CLI:

```powershell
dotnet tool install --global dotnet-ef
```

### 4. Chạy ứng dụng

```powershell
dotnet run `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj
```

Mở URL được hiển thị trong terminal, ví dụ:

```text
http://localhost:5198
```

## Kiểm thử

### Chạy toàn bộ test

```powershell
dotnet test src/backend/ShiftScheduling.sln `
  --no-build `
  --verbosity normal
```

### Kiểm tra toàn project

```powershell
powershell -ExecutionPolicy Bypass `
  -File src/backend/verify_project.ps1
```

Baseline hiện tại:

```text
36 tests
36 passed
0 failed
0 skipped

Build:
0 warnings
0 errors

PROJECT VERIFY: PASS
```

## Tài khoản demo

| Vai trò | Username | Password | Quyền chính |
|---|---|---|---|
| Admin | `admin` | `admin123` | Tài khoản, nhân viên |
| Manager | `quanly` | `quanly123` | Nhân viên, ca làm, kỳ lịch |

> Tài khoản demo Employee chưa được seed trong phiên bản hiện tại.

## Quy tắc nghiệp vụ chính đã triển khai

- Một kỳ lịch kéo dài đúng 7 ngày.
- Hệ thống sử dụng đúng 3 ca cố định.
- Mỗi kỳ lịch có đúng 21 tổ hợp ngày-ca.
- Định mức nhân sự được cấu hình theo vị trí Tư vấn và Thu ngân.
- Không cho phép định mức âm.
- Tổng nhân sự của một tổ hợp ngày-ca phải lớn hơn 0.
- Mỗi ca phải đáp ứng yêu cầu nhân sự có Level 2 trở lên theo cấu hình hiện tại.
- Chỉ Manager được cấu hình ca làm và kỳ lịch.
- Admin được quản lý tài khoản và nhân viên.

## Trạng thái chức năng

| Mã | Chức năng | Trạng thái |
|---|---|---|
| FR01 / UC01 | Quản lý tài khoản | ✅ Hoàn thành |
| FR02 / UC02 | Quản lý nhân viên | ✅ Hoàn thành |
| FR03 / UC03 | Kỳ lịch & định mức nhân sự | ✅ Hoàn thành |
| FR04 / UC04 | Đăng ký lịch rảnh | ⏳ Tiếp theo |
| FR05+ | Phân công, OR-Tools, nghỉ, đổi ca, báo cáo | ⏳ Chưa triển khai |


## Repository

- **GitHub:** https://github.com/binhnt1308/employee-shift-scheduling-system
- **Branch hiện tại:** `tuan-05`

## Thông tin sinh viên

- **Sinh viên thực hiện:** Nguyễn Thanh Bình
- **MSSV:** 2354050017
- **Giảng viên hướng dẫn:** Th.S Võ Việt Khoa
