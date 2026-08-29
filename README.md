# Employee Shift Scheduling & Workforce Management System

## Tên đề tài

- **Tên tiếng Anh:** Employee Shift Scheduling System
- **Tên tiếng Việt:** Hệ thống lập lịch và quản lý phân công ca làm việc cho nhân viên

## Mô tả ngắn

Hệ thống web hỗ trợ quản lý nhân viên và lập lịch ca làm việc cho cửa hàng bán lẻ quy mô nhỏ
(khoảng 10–20 nhân viên).

Hệ thống hỗ trợ từ quản lý tài khoản, nhân viên, kỳ lịch, đăng ký lịch rảnh,
phân công ca thủ công, sinh lịch bằng Google OR-Tools, công bố lịch,
xin nghỉ, đổi ca đến báo cáo số ca và giờ làm việc.

---

## Chức năng đã hoàn thành

- Đăng nhập / đăng xuất bằng Cookie Authentication.
- Hash mật khẩu bằng PBKDF2.
- Quản lý tài khoản, khóa / mở khóa và phân quyền.
- Quản lý nhân viên.
- Quản lý 3 ca cố định:
  - Ca sáng: `08:00–12:00`
  - Ca chiều: `12:00–16:00`
  - Ca tối: `16:00–20:00`
- Thiết lập kỳ lịch 7 ngày.
- Tự động tạo `7 ngày × 3 ca = 21` tổ hợp ngày-ca.
- Cấu hình định mức nhân sự theo vị trí Tư vấn / Thu ngân.
- Kiểm tra yêu cầu nhân viên Level 2 trở lên.
- Đăng ký và cập nhật lịch rảnh của Employee.
- Phân công ca thủ công:
  - Thêm nhân viên.
  - Xóa nhân viên.
  - Thay thế nhân viên.
- Sinh lịch đề xuất bằng Google OR-Tools CP-SAT.
- Kiểm tra ràng buộc trước khi phân công và công bố lịch.
- Lưu lịch Draft và công bố lịch chính thức.
- Employee xem lịch làm việc đã công bố.
- Quản lý đơn xin nghỉ.
- Quản lý yêu cầu đổi ca.
- Báo cáo số ca, số ngày và tổng giờ làm.
- Xuất báo cáo CSV.

---

## Quy trình lập lịch

```text
Manager tạo kỳ lịch
        ↓
Cấu hình định mức
        ↓
Mở đăng ký lịch rảnh
        ↓
Employee đăng ký lịch rảnh
        ↓
Manager đóng đăng ký
        ↓
┌─────────────────────────┐
│ Phân công ca thủ công   │
│ Add / Remove / Replace  │
└─────────────────────────┘
            hoặc
┌─────────────────────────┐
│ Google OR-Tools CP-SAT  │
│ Sinh lịch đề xuất       │
└─────────────────────────┘
        ↓
Draft
        ↓
Kiểm tra ràng buộc
        ↓
Publish
        ↓
Employee xem lịch
```

> Google OR-Tools chỉ sinh lịch đề xuất. Manager phải chủ động sử dụng đề xuất,
> chỉnh sửa nếu cần và công bố lịch.

---

## Trạng thái chức năng

| Mã | Chức năng | Trạng thái |
| --- | --- | --- |
| UC01 | Quản lý tài khoản | ✅ Hoàn thành |
| UC02 | Quản lý nhân viên | ✅ Hoàn thành |
| UC03 | Kỳ lịch & định mức nhân sự | ✅ Hoàn thành |
| UC04 | Quản lý lịch rảnh | ✅ Hoàn thành |
| UC05 | Phân công ca thủ công | ✅ Hoàn thành |
| UC06 | Sinh lịch bằng Google OR-Tools | ✅ Hoàn thành |
| UC07 | Duyệt và công bố lịch | ✅ Hoàn thành |
| UC08 | Xem lịch làm việc cá nhân | ✅ Hoàn thành |
| UC09 | Quản lý đơn xin nghỉ | ✅ Hoàn thành một phần |
| UC10 | Quản lý yêu cầu đổi ca | ✅ Hoàn thành |
| UC11 | Báo cáo số ca và giờ làm | ✅ Hoàn thành |

---

## Phân quyền

| Chức năng | Admin | Manager | Employee |
| --- | :---: | :---: | :---: |
| Quản lý tài khoản | ✅ | ❌ | ❌ |
| Quản lý nhân viên | ✅ | ✅ | ❌ |
| Cấu hình ca / kỳ lịch | ❌ | ✅ | ❌ |
| Đăng ký lịch rảnh | ❌ | ❌ | ✅ |
| Xem tổng hợp lịch rảnh | ❌ | ✅ | ❌ |
| Phân công lịch | ❌ | ✅ | ❌ |
| Sinh lịch bằng OR-Tools | ❌ | ✅ | ❌ |
| Công bố lịch | ❌ | ✅ | ❌ |
| Xem lịch cá nhân | ❌ | ❌ | ✅ |
| Gửi đơn xin nghỉ | ❌ | ❌ | ✅ |
| Duyệt đơn xin nghỉ | ❌ | ✅ | ❌ |
| Gửi / phản hồi đổi ca | ❌ | ❌ | ✅ |
| Duyệt đổi ca | ❌ | ✅ | ❌ |
| Báo cáo tổng hợp | ❌ | ✅ | ❌ |
| Báo cáo cá nhân | ❌ | ❌ | ✅ |

---

## Một số quy tắc nghiệp vụ chính

- Một kỳ lịch kéo dài đúng 7 ngày.
- Hệ thống sử dụng đúng 3 ca cố định.
- Mỗi kỳ lịch có đúng 21 tổ hợp ngày-ca.
- Không cho phép tạo trùng kỳ lịch cùng tuần.
- Employee chỉ đăng ký lịch rảnh khi kỳ lịch đang mở và chưa hết hạn.
- Chỉ phân công nhân viên đang hoạt động.
- Nhân viên phải đăng ký rảnh trước khi được phân công.
- Nhân viên có đơn nghỉ đã duyệt không được phân vào ca nghỉ.
- Phân công phải đúng vị trí công việc.
- PartTime tối đa 1 ca/ngày.
- FullTime làm hai ca liên tiếp sáng–chiều hoặc chiều–tối.
- FullTime tối đa 6 ngày/tuần.
- Mỗi ca phải đáp ứng định mức nhân sự theo vị trí.
- Mỗi ca phải có ít nhất một nhân viên Level 2 trở lên.
- Draft có thể lưu khi chưa hoàn chỉnh nhưng không được Publish nếu còn lỗi cứng.
- OR-Tools chỉ tạo lịch đề xuất.
- Trước khi Publish, hệ thống kiểm tra lại dữ liệu mới nhất.
- Đổi ca là chuyển ca một chiều.
- Các mục tiêu cân bằng khối lượng công việc là ràng buộc mềm và không được phá ràng buộc cứng.

---

## Công nghệ sử dụng

- **Backend:** ASP.NET Core MVC 8 (.NET 8, C#)
- **Frontend:** Razor View, Bootstrap 5, HTML5, CSS3, JavaScript
- **ORM:** Entity Framework Core 8
- **Database:** SQL Server
- **Authentication:** Cookie Authentication
- **Password Hashing:** PBKDF2
- **Scheduling Engine:** Google OR-Tools CP-SAT
- **Testing:** xUnit
- **Version Control:** Git, GitHub

---

## Kiến trúc backend

Backend được tổ chức theo Feature:

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
## Kiểm thử gần nhất

Automated test được chạy trên branch `tuan-07`.

```text
Tests:
140 total
140 passed
0 failed
0 skipped

Các nhóm kiểm thử chính:

Authentication và Authorization.
Employee lifecycle.
Kỳ lịch và 3 ca cố định.
Availability.
Phân công thủ công.
Quy tắc Part-time / Full-time.
BR20 — nghỉ tối thiểu 12 giờ.
BR21 — nghỉ liên tục tối thiểu 24 giờ.
OR-Tools:
Phương án khả thi.
INFEASIBLE.
Proposal-only.
Không tự động Publish.
Publish validation.
Đơn xin nghỉ.
Đổi ca.
Báo cáo và xuất CSV.
Concurrency.
Các trường hợp biên ngày tháng.

Lưu ý: Kết quả automated test và kết quả kiểm thử HTTP/UI được quản lý độc lập.

Không tự động xem testcase HTTP/UI là Pass chỉ vì automated test tương ứng đang Pass.

Tài liệu kiểm thử

Các tài liệu kiểm thử chính:

docs/kiem-thu/
├── testcase.md
├── matran-truyvet.md
└── BR_TEST_COVERAGE_TUAN07.md

Trong đó:

testcase.md: Mô tả chi tiết các testcase chính.
matrantruyvet.md: Truy vết FR / BR → testcase theo TC-08.
BR_Test Coverage.md: Kiểm tra nhanh coverage theo từng Business Rule.
Kỹ thuật kiểm thử

Các kỹ thuật kiểm thử được sử dụng:

Phân hoạch tương đương.
Phân tích giá trị biên.
Bảng quyết định.
Kiểm thử chuyển trạng thái.
Kiểm thử hồi quy.
---
Các luồng chính đã được kiểm tra:

- Đăng ký và cập nhật lịch rảnh.
- Phân công thủ công Add / Remove / Replace.
- Kiểm tra các ràng buộc phân ca.
- OR-Tools sinh lịch hợp lệ.
- OR-Tools xử lý trường hợp `INFEASIBLE`.
- Chặn Publish khi Draft chưa hợp lệ.
- Kiểm tra lại dữ liệu mới nhất trước khi Publish.
- Phân quyền theo vai trò.
- Đơn xin nghỉ.
- Đổi ca.
- Báo cáo và xuất CSV.

---

## Cài đặt và chạy

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

### 3. Cập nhật database

```powershell
dotnet ef database update `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj
```

Nếu chưa có Entity Framework CLI:

```powershell
dotnet tool install --global dotnet-ef
```

### 4. Chạy ứng dụng

```powershell
dotnet run `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj
```

Mở URL được hiển thị trong terminal.

---

## Chạy kiểm thử

```powershell
dotnet format src/backend/ShiftScheduling.sln --verify-no-changes

dotnet build src/backend/ShiftScheduling.sln `
  --no-restore `
  --warnaserror

dotnet test src/backend/ShiftScheduling.sln `
  --no-build `
  --verbosity normal
```

---

## Tài khoản demo

| Vai trò | Username | Password |
| --- | --- | --- |
| Admin | `admin` | `admin123` |
| Manager | `quanly` | `quanly123` |

> Tài khoản Employee hiện được sử dụng trong dữ liệu kiểm thử.
> Dữ liệu seed Employee chính thức sẽ được hoàn thiện sau.

---

## Công việc tiếp theo

- Hoàn thiện dữ liệu demo.
- Hoàn thiện ma trận truy vết yêu cầu – test case.
- Bổ sung kiểm thử E2E.
- Hoàn thiện giao diện và responsive.
- Docker hóa ứng dụng.
- Thiết lập CI/CD.
- Chuẩn bị triển khai và demo bảo vệ.

---

## Repository

- **GitHub:** https://github.com/binhnt1308/employee-shift-scheduling-system
- **Branch phát triển:** `tuan-05`

---

## Thông tin sinh viên

- **Sinh viên thực hiện:** Nguyễn Thanh Bình
- **MSSV:** 2354050017
- **Giảng viên hướng dẫn:** Th.S Võ Việt Khoa
