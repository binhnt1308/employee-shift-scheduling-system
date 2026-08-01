# Employee Shift Scheduling & Workforce Management System

## Tên đề tài

**Tên tiếng Anh:** Employee Shift Scheduling & Workforce Management System  
**Tên tiếng Việt:** Hệ thống lập lịch và quản lý phân công ca làm việc cho nhân viên

## Giới thiệu

Đề tài xây dựng hệ thống web hỗ trợ lập lịch làm việc cho một cửa hàng bán lẻ quy mô nhỏ, gồm khoảng 10–20 nhân viên thuộc hai vị trí là nhân viên tư vấn và nhân viên thu ngân.

Hệ thống cho phép nhân viên đăng ký lịch rảnh, xem lịch đã công bố, gửi đơn xin nghỉ và yêu cầu đổi ca. Quản lý có thể quản lý nhân viên, ca làm việc, phân công ca, tạo lịch đề xuất và công bố lịch chính thức.

Google OR-Tools CP-SAT được sử dụng ở mức cơ bản để hỗ trợ tạo lịch đề xuất. Quản lý vẫn là người kiểm tra, điều chỉnh và quyết định lịch cuối cùng.

## Chức năng chính

1. Quản lý tài khoản và phân quyền.
2. Quản lý nhân viên.
3. Quản lý ca làm việc.
4. Quản lý lịch rảnh.
5. Quản lý lịch làm việc.
6. Quản lý đơn xin nghỉ.
7. Quản lý yêu cầu đổi ca.
8. Tổng hợp số ca và giờ làm.

## Công nghệ sử dụng

- C# / .NET 8
- ASP.NET Core MVC
- Razor View và Bootstrap 5
- Entity Framework Core
- SQL Server LocalDB
- Cookie Authentication
- Git và GitHub

## Kiến trúc hệ thống

```text
Razor View
    ↓
Controller
    ↓
Service
    ↓
Entity Framework Core
    ↓
SQL Server
```

## Chức năng hiện có

- Đăng nhập, đăng xuất và phân quyền.
- Quản lý tài khoản.
- Quản lý nhân viên.
- Quản lý ca làm việc.
- Tạo cơ sở dữ liệu từ 11 Entity bằng EF Core Code First.
- Khởi tạo tài khoản và ba ca làm việc mặc định.

## Tài khoản mặc định

| Vai trò | Username | Password |
|---|---|---|
| Admin | `admin` | `admin123` |
| Quản lý | `quanly` | `quanly123` |

## Ca làm việc mặc định

| Ca | Thời gian |
|---|---|
| Ca sáng | 08:00–12:00 |
| Ca chiều | 12:00–16:00 |
| Ca tối | 16:00–20:00 |

## Hướng dẫn chạy

1. Mở file `ShiftScheduling.sln`.
2. Chờ Visual Studio hoàn thành NuGet Restore.
3. Mở Package Manager Console.
4. Chọn project `ShiftScheduling`.
5. Chạy lệnh:

```powershell
Update-Database
```

6. Nhấn `Ctrl + F5` để chạy project.

## Chức năng đang phát triển

- Kỳ lịch và định mức nhân sự.
- Lịch rảnh.
- Phân công và công bố lịch.
- Đơn xin nghỉ.
- Yêu cầu đổi ca.
- Tổng hợp giờ làm.
- Google OR-Tools.
- Docker, CI/CD và triển khai website.

## Tác giả

**Sinh viên:** Nguyễn Thanh Bình  
**MSSV:** 2354050017  
**GVHD:** Th.S Võ Việt Khoa
