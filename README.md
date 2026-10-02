# Employee Shift Scheduling System

## 1. Giới thiệu

**Tên tiếng Việt:** Hệ thống lập lịch và quản lý phân công ca làm việc cho nhân viên  
**Tên tiếng Anh:** Employee Shift Scheduling System

**Phiên bản bàn giao:** `v1.0`

Employee Shift Scheduling System là ứng dụng web hỗ trợ quản lý nhân viên và lập lịch làm việc theo tuần cho một cửa hàng bán lẻ quy mô nhỏ.

Hệ thống hỗ trợ toàn bộ quy trình từ tạo kỳ lịch, đăng ký lịch rảnh, phân công nhân viên, sinh lịch đề xuất bằng Google OR-Tools CP-SAT, công bố lịch, xử lý đơn nghỉ và chuyển ca, đến tổng hợp giờ làm và chốt kỳ.

Google OR-Tools chỉ tạo **phương án lịch đề xuất**. Quản lý vẫn là người kiểm tra, lựa chọn, điều chỉnh và công bố lịch chính thức.

---

## 2. Công nghệ sử dụng

- **Backend:** ASP.NET Core MVC
- **Target framework:** .NET 8 (`net8.0`)
- **Ngôn ngữ:** C#
- **Frontend:** Razor View, Bootstrap 5, HTML5, CSS3, JavaScript
- **ORM:** Entity Framework Core 8
- **Database:** Microsoft SQL Server
- **Authentication:** Cookie Authentication
- **Password hashing:** PBKDF2
- **Scheduling engine:** Google OR-Tools CP-SAT
- **Testing:** xUnit
- **CI:** GitHub Actions
- **Containerization:** Docker, Docker Compose
- **Version control:** Git, GitHub

---

## 3. Chức năng chính

| Chức năng | Trạng thái |
| --- | --- |
| Đăng nhập / đăng xuất | ✅ Hoàn thành |
| Quản lý tài khoản và phân quyền | ✅ Hoàn thành |
| Quản lý nhân viên | ✅ Hoàn thành |
| Quản lý kỳ lịch | ✅ Hoàn thành |
| Cấu hình ba ca làm việc theo phiên bản | ✅ Hoàn thành |
| Cấu hình định mức nhân sự | ✅ Hoàn thành |
| Đăng ký lịch rảnh | ✅ Hoàn thành |
| Mẫu lịch rảnh theo tuần | ✅ Hoàn thành |
| Mức ưu tiên Normal / Preferred | ✅ Hoàn thành |
| Phân công ca thủ công | ✅ Hoàn thành |
| Sinh lịch đề xuất bằng Google OR-Tools | ✅ Hoàn thành |
| Kiểm tra ràng buộc trước khi công bố | ✅ Hoàn thành |
| Lưu lịch Draft | ✅ Hoàn thành |
| Công bố lịch | ✅ Hoàn thành |
| Theo dõi trạng thái lịch đang áp dụng | ✅ Hoàn thành |
| Chốt kỳ sau khi kỳ kết thúc | ✅ Hoàn thành |
| Khóa chỉnh sửa lịch đã Closed | ✅ Hoàn thành |
| Nhân viên xem lịch cá nhân | ✅ Hoàn thành |
| Quản lý đơn xin nghỉ | ✅ Hoàn thành |
| Bố trí người thay cho đơn nghỉ | ✅ Hoàn thành |
| Duyệt nghỉ khẩn cấp | ✅ Hoàn thành |
| Quản lý yêu cầu chuyển ca | ✅ Hoàn thành |
| Điều chỉnh giờ làm thực tế | ✅ Hoàn thành |
| Báo cáo số ca, số ngày và giờ làm | ✅ Hoàn thành |
| Theo dõi độ phủ nhân sự | ✅ Hoàn thành |
| Báo cáo fairness FT / PT | ✅ Hoàn thành |
| Xuất báo cáo CSV | ✅ Hoàn thành |
| Thông báo trong hệ thống | ✅ Hoàn thành |
| Email notification | ✅ Có hỗ trợ, cần cấu hình SMTP để gửi email thật |

---

## 4. Vai trò người dùng

Hệ thống sử dụng ba vai trò:

- **Admin**
- **Manager**
- **Employee**

| Chức năng | Admin | Manager | Employee |
| --- | :---: | :---: | :---: |
| Quản lý tài khoản | ✅ | ❌ | ❌ |
| Quản lý nhân viên | ✅ | ✅ | ❌ |
| Quản lý kỳ lịch | ❌ | ✅ | ❌ |
| Cấu hình ca và định mức nhân sự | ❌ | ✅ | ❌ |
| Đăng ký lịch rảnh | ❌ | ❌ | ✅ |
| Xem tổng hợp lịch rảnh | ❌ | ✅ | ❌ |
| Phân công ca | ❌ | ✅ | ❌ |
| Sinh lịch bằng OR-Tools | ❌ | ✅ | ❌ |
| Công bố lịch | ❌ | ✅ | ❌ |
| Chốt kỳ | ❌ | ✅ | ❌ |
| Xem lịch cá nhân | ❌ | ❌ | ✅ |
| Gửi / hủy đơn xin nghỉ | ❌ | ❌ | ✅ |
| Xử lý đơn xin nghỉ | ❌ | ✅ | ❌ |
| Bố trí người thay | ❌ | ✅ | ❌ |
| Gửi / phản hồi yêu cầu chuyển ca | ❌ | ❌ | ✅ |
| Duyệt yêu cầu chuyển ca | ❌ | ✅ | ❌ |
| Điều chỉnh giờ làm thực tế | ❌ | ✅ | ❌ |
| Báo cáo tổng hợp | ❌ | ✅ | ❌ |
| Báo cáo cá nhân | ❌ | ❌ | ✅ |
| Xuất CSV | ❌ | ✅ | ❌ |

---

## 5. Quy trình lập lịch

Quy trình chính của hệ thống:

```text
Manager tạo kỳ lịch
        ↓
Cấu hình định mức nhân sự
        ↓
Mở đăng ký lịch rảnh
        ↓
Employee đăng ký lịch rảnh
        ↓
Hết thời hạn đăng ký
        ↓
Manager lập lịch
   ↙             ↘
Thủ công       OR-Tools
                  ↓
           Lịch đề xuất
                  ↓
       Manager chọn sử dụng
           ↘       ↙
              Draft
                ↓
      Kiểm tra ràng buộc
                ↓
             Publish
                ↓
      Employee xem lịch
                ↓
  Xử lý nghỉ / chuyển ca
                ↓
 Báo cáo / giờ làm thực tế
                ↓
          Kết thúc kỳ
                ↓
              Close
```

OR-Tools không tự động công bố lịch.

Manager phải chủ động chọn sử dụng Proposal, sau đó có thể tiếp tục chỉnh sửa Draft trước khi Publish.

---

## 6. Vòng đời lịch làm việc

Vòng đời nghiệp vụ được biểu diễn như sau:

```text
Draft
  ↓
Published
  ↓
Active
  ↓
Closed
```

### Draft

Lịch đang được Manager chuẩn bị.

Manager có thể:

- phân công thủ công;
- xóa hoặc thay nhân viên;
- sử dụng lịch đề xuất từ OR-Tools;
- kiểm tra các ràng buộc;
- lưu lịch chưa hoàn chỉnh.

Draft chưa được Employee nhìn thấy.

### Published

Lịch đã được kiểm tra và công bố chính thức.

Employee có thể xem lịch và thực hiện các nghiệp vụ như:

- xin nghỉ;
- chuyển ca;
- xem lịch cá nhân.

### Active

`Active` là trạng thái nghiệp vụ **được suy ra** khi lịch đang ở `Published` và ngày hiện tại đã bước vào khoảng thời gian của kỳ.

`Active` không phải một trạng thái riêng cần lưu trong database.

### Closed

Sau khi kỳ kết thúc, Manager có thể chốt kỳ.

Lịch `Closed`:

- không cho phân công hoặc xóa nhân viên;
- không cho áp dụng Proposal OR-Tools;
- không cho Save Draft;
- không cho chuyển ca;
- không cho điều chỉnh hoặc reset giờ làm;
- vẫn cho xem lịch sử;
- vẫn cho xem Manager Report;
- vẫn cho xem My Report;
- vẫn cho xuất CSV.

Việc đóng đăng ký lịch rảnh của `SchedulePeriod` và trạng thái `Closed` của `WorkSchedule` là hai khái niệm khác nhau.

---

# 7. Chạy project

## 7.1. Cách khuyến nghị: Docker Compose

Docker Compose là cách chạy được khuyến nghị khi bàn giao vì có thể dựng đồng thời:

- ASP.NET Core Web;
- SQL Server;
- database persistent bằng Docker volume.

Máy chạy không cần SQL Server LocalDB.

### Yêu cầu

- Git
- Docker Desktop hoặc Docker Engine
- Docker Compose

### Chạy nhanh từ source mới clone

Mở PowerShell:

```powershell
git clone https://github.com/binhnt1308/employee-shift-scheduling-system.git
cd employee-shift-scheduling-system
Copy-Item .env.example .env
docker compose up --build -d
Start-Process "http://localhost:8080"
```

Ứng dụng chạy tại:

```text
http://localhost:8080
```

Luồng khởi động:

```text
SQL Server container
        ↓
SQL Server sẵn sàng
        ↓
ASP.NET Core Web khởi động
        ↓
EF Core áp dụng migration còn thiếu
        ↓
DemoDataSeeder kiểm tra dữ liệu
        ↓
Seed dữ liệu demo nếu database mới
        ↓
Website sẵn sàng
```

Kiểm tra container:

```powershell
docker compose ps
```

Xem log Web:

```powershell
docker compose logs --tail 150 web
```

Xem log SQL Server:

```powershell
docker compose logs --tail 100 db
```

Dừng hệ thống:

```powershell
docker compose down
```

Lệnh trên **không xóa database** vì dữ liệu SQL Server được lưu bằng named volume.

Không sử dụng:

```powershell
docker compose down -v
```

nếu muốn giữ database hiện tại.

Tùy chọn `-v` sẽ xóa Docker volume và toàn bộ database trong volume đó.

---

## 7.2. Cấu hình Docker

Docker Compose sử dụng file `.env` cho các cấu hình local.

Tạo file:

```powershell
Copy-Item .env.example .env
```

Các biến chính:

```text
MSSQL_SA_PASSWORD
ASPNETCORE_ENVIRONMENT
DEMO_DATA_ENABLED
```

`.env.example` được commit để mô tả cấu hình cần thiết.

`.env` thuộc từng máy và **không được commit**.

Ứng dụng nhận connection string thông qua:

```text
ConnectionStrings__DefaultConnection
```

Docker Compose cấu hình Web kết nối SQL Server bằng TCP qua service `db`.

Database demo chuẩn:

```text
ShiftSchedulingDb_Demo
```

Database này được tách biệt với:

```text
ShiftSchedulingDb
```

và database dành riêng cho integration test:

```text
ShiftSchedulingDb_IntegrationTest
```

---

## 7.3. Persistence

SQL Server sử dụng Docker named volume.

Do đó các thao tác:

```powershell
docker compose restart
```

hoặc:

```powershell
docker compose down
docker compose up -d
```

không làm mất database nếu không sử dụng `-v`.

Việc restart hoặc redeploy ứng dụng cũng **không tự động reset dữ liệu demo**.

---

# 8. Dữ liệu demo

DemoDataSeeder tạo dữ liệu tương đối theo ngày hiện tại tại thời điểm database được seed.

Dữ liệu không tự động trượt sang tuần mới sau mỗi lần restart.

Fresh demo database gồm:

- 8 Employee;
- 2 Position;
- 5 FullTime;
- 3 PartTime;
- 3 ShiftType;
- StaffingRequirement;
- Availability;
- WorkSchedule;
- ShiftAssignment;
- dữ liệu phục vụ report và các nghiệp vụ chính.

## 8.1. Nhân viên

Fresh database có:

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

---

## 8.2. Ba ca làm việc

```text
MORNING     08:00 - 12:00
AFTERNOON   12:00 - 16:00
EVENING     16:00 - 20:00
```

---

## 8.3. Dataset 4 kỳ

Seeder tạo **4 kỳ tương đối với tuần hiện tại theo giờ Việt Nam**.

### Kỳ 1 — Tuần trước

```text
Registration: Closed
Schedule: Closed
```

Dữ liệu gồm:

- 42 ShiftAssignment;
- lịch sử làm việc hoàn chỉnh;
- dữ liệu Manager Report;
- dữ liệu My Report;
- dữ liệu CSV;
- dữ liệu điều chỉnh giờ thực tế.

Ví dụ:

```text
Employee: NV02 - Chiêm Trung Hậu
Scheduled hours: 4.00
Actual worked hours: 3.50
```

Kỳ này dùng để demo:

- lịch sử;
- báo cáo;
- giờ công thực tế;
- trạng thái Closed;
- dữ liệu không còn được chỉnh sửa.

### Kỳ 2 — Tuần hiện tại

```text
Registration: Closed
Schedule persisted status: Published
UI effective status: Active / Đang áp dụng
```

Dữ liệu gồm:

- 42 ShiftAssignment;
- lịch cá nhân;
- đơn nghỉ;
- chuyển ca;
- report;
- các nghiệp vụ của lịch đang áp dụng.

`Active` được suy ra từ `Published` và thời gian hiện tại.

### Kỳ 3 — Tuần kế tiếp

```text
Registration status: Open
Availability deadline: đã hết hạn
Schedule: Draft
Assignments: 0
```

Dữ liệu gồm:

- 21 StaffingRequirement;
- Availability của 8 nhân viên;
- lịch Draft trống ban đầu.

Manager có thể dùng kỳ này để demo:

```text
Xem lịch rảnh
→ phân công thủ công

hoặc

→ chạy OR-Tools
→ xem Proposal
→ áp dụng Proposal
→ tiếp tục chỉnh Draft
→ Publish
```

### Kỳ 4 — Tuần sau nữa

```text
Registration: Open
Availability deadline: trong tương lai
WorkSchedule: chưa tạo
```

Dữ liệu gồm:

- 21 StaffingRequirement;
- dữ liệu Availability / mẫu đăng ký phù hợp;
- chưa có WorkSchedule.

Kỳ này dùng để demo:

```text
Employee đăng nhập
→ đăng ký lịch rảnh
→ chọn Normal / Preferred
→ lưu lịch rảnh
```

---

# 9. Làm mới dữ liệu demo an toàn

Dataset demo được tính theo ngày tại **thời điểm seed**.

Ứng dụng không tự reset dữ liệu khi:

- restart Web;
- restart Docker;
- `docker compose down`;
- deploy lại source.

Khi cần tạo lại 4 kỳ theo thời điểm hiện tại, sử dụng `reset-demo`.

## 9.1. Reset bằng Docker

Dừng Web:

```powershell
docker compose stop web
```

Chạy reset:

```powershell
docker compose run --rm `
  -e DemoData__AllowReset=true `
  web `
  --reset-demo=ShiftSchedulingDb_Demo
```

Khởi động Web lại:

```powershell
docker compose up -d web
```

Sau đó truy cập:

```text
http://localhost:8080
```

---

## 9.2. Các chốt an toàn của reset-demo

Database chỉ được xóa khi **tất cả** điều kiện sau đúng:

1. Có tham số:

```text
--reset-demo=<DATABASE_NAME>
```

2. `DemoData:Enabled = true`.

3. `DemoData:AllowReset = true`.

4. Database thực tế trong connection string có tên hợp lệ.

5. Tên trong `--reset-demo` phải khớp database thực tế.

6. Database phải có hậu tố:

```text
_Demo
```

Ví dụ hợp lệ:

```text
ShiftSchedulingDb_Demo
```

Các database như sau không được reset:

```text
ShiftSchedulingDb
ShiftSchedulingDb_IntegrationTest
master
```

---

## 9.3. Reset thực hiện những gì?

```text
Kiểm tra các chốt an toàn
        ↓
EnsureDeletedAsync()
        ↓
MigrateAsync()
        ↓
DemoDataSeeder.SeedAsync()
        ↓
Tạo lại dataset 4 kỳ theo ngày hiện tại
        ↓
Tiến trình reset kết thúc
```

Lệnh reset không khởi động Web server.

Sau reset phải chạy ứng dụng bình thường lại.

> `reset-demo` là thao tác phá hủy dữ liệu. Chỉ sử dụng với database demo riêng.

`DemoData__AllowReset=true` không được cấu hình mặc định trong repository hoặc môi trường production.

---

# 10. Tài khoản demo

Fresh demo database sử dụng các tài khoản:

| Username | Vai trò |
| --- | --- |
| `admin` | Admin |
| `quanly` | Manager |
| `nv01` | Employee |
| `nv02` | Employee |
| `nv03` | Employee |
| `nv04` | Employee |
| `nv05` | Employee |
| `nv06` | Employee |
| `nv07` | Employee |
| `nv08` | Employee |

Mật khẩu demo:

```text
Password123!
```

Ví dụ Employee dùng cho nhiều luồng demo:

```text
Username: nv02
Password: Password123!
Employee: NV02 - Chiêm Trung Hậu
```

Tài khoản legacy:

```text
nhanvien
```

không còn được sử dụng trên fresh demo database.

---

# 11. Chạy trực tiếp trên Windows

Docker là cách khuyến nghị khi bàn giao.

Trong môi trường phát triển Windows, ứng dụng vẫn có thể chạy trực tiếp bằng LocalDB.

## Yêu cầu

- Windows 10/11
- .NET 8 SDK
- SQL Server Express LocalDB
- PowerShell

Restore:

```powershell
dotnet restore src/backend/ShiftScheduling.sln
```

Khởi động LocalDB:

```powershell
sqllocaldb start MSSQLLocalDB
```

Cấu hình database demo:

```powershell
$env:ConnectionStrings__DefaultConnection = `
"Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDb_Demo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"

$env:DemoData__Enabled = "true"
```

Chạy:

```powershell
dotnet run `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj `
  --configuration Release
```

Ứng dụng sẽ:

```text
Kết nối SQL Server
→ Migrate database
→ Seed demo nếu cần
→ khởi động Web
```

LocalDB chỉ là lựa chọn cho môi trường phát triển Windows.

Docker, CI và môi trường triển khai không phụ thuộc LocalDB.

---

# 12. Migration

Schema được quản lý bằng **Entity Framework Core Migrations**.

Khi ứng dụng khởi động:

```text
Database.MigrateAsync()
```

được sử dụng để áp dụng các migration còn thiếu.

Có thể chạy thủ công:

```powershell
dotnet ef database update `
  --project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj `
  --startup-project src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj
```

Nếu chưa có `dotnet-ef`:

```powershell
dotnet tool install --global dotnet-ef --version "8.*"
```

Không chỉnh schema production trực tiếp bằng SSMS thay cho migration.

---

# 13. Cấu hình theo môi trường

Source code không cần thay đổi giữa:

```text
Windows local
Docker
CI
Deployment
```

Khác biệt giữa các môi trường được cung cấp bằng:

- Environment Variables;
- User Secrets;
- configuration/secrets của nền tảng deploy.

Không commit:

- `.env`;
- production connection string;
- SMTP password;
- App Password;
- API key;
- credential thật.

---

## 13.1. Connection string

Biến:

```text
ConnectionStrings__DefaultConnection
```

Ví dụ LocalDB:

```powershell
$env:ConnectionStrings__DefaultConnection = `
"Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDb_Demo;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Docker Compose tự cung cấp connection string sử dụng:

```text
Server=db,1433
Database=ShiftSchedulingDb_Demo
```

---

## 13.2. Demo data

Biến:

```text
DemoData__Enabled
```

Ví dụ:

```powershell
$env:DemoData__Enabled = "true"
```

---

## 13.3. SMTP

Email là kênh bổ sung.

Nghiệp vụ chính được lưu trước khi gửi email.

Nếu SMTP lỗi:

- kết quả Publish không bị rollback;
- kết quả xử lý nghỉ không bị rollback;
- kết quả chuyển ca không bị rollback.

Cấu hình tham khảo:

```text
src/backend/ShiftScheduling.Web/appsettings.Example.json
```

Không commit SMTP password hoặc App Password thật.

---

# 14. Kiến trúc backend

Backend được tổ chức chủ yếu theo Feature.

Luồng xử lý:

```text
Razor View
    ↓
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
├── Notifications/
├── Reports/
├── SchedulePeriods/
├── ShiftTransfers/
├── ShiftTypes/
└── WorkSchedules/
```

OR-Tools được tách thành các service chuyên trách trong feature WorkSchedules.

---

# 15. Một số quy tắc nghiệp vụ chính

- Một kỳ lịch kéo dài 7 ngày.
- Hệ thống sử dụng ba mã ca Morning, Afternoon và Evening.
- Một kỳ có 21 tổ hợp ngày–ca.
- Chỉ nhân viên đang hoạt động được tham gia lịch mới.
- Nhân viên phải đăng ký rảnh trước khi được phân công.
- Approved Leave chặn phân công tương ứng.
- Phân công phải đúng vị trí công việc.
- Mỗi ca phải đáp ứng định mức theo Position.
- Mỗi ca phải có ít nhất một nhân viên Level 2 trở lên.
- PartTime tối đa 1 ca/ngày.
- PartTime có hard cap 28 giờ/tuần.
- FullTime tối đa 6 ngày/tuần.
- FullTime có hard cap 48 giờ/tuần.
- Khoảng nghỉ giữa ngày làm việc phải đạt tối thiểu 12 giờ.
- Mỗi nhân viên phải có ít nhất 24 giờ nghỉ liên tục trong kỳ.
- Draft có thể chưa hoàn chỉnh.
- Không Publish khi còn lỗi bắt buộc.
- OR-Tools chỉ tạo Proposal.
- Trước Publish, hệ thống tải dữ liệu mới nhất và validation lại.
- `Preferred` được ưu tiên hơn `Normal`.
- Fairness của OR-Tools ưu tiên giảm chênh lệch tổng phút làm việc giữa nhân viên cùng nhóm FT/PT.
- Hệ thống ưu tiên mềm việc FT được xếp các ca liền nhau khi phù hợp.
- Hệ thống hạn chế ca Evening ở hai ngày liên tiếp khi có thể.
- Chuyển ca là chuyển một ca một chiều, không phải hoán đổi hai ca.
- Manager chỉ được chỉnh giờ thực tế sau khi ca kết thúc.
- Approved Leave mặc định tính 0 giờ.
- Giá trị điều chỉnh khác số giờ hệ thống phải có lý do.
- Sau khi lịch Closed, dữ liệu chỉ đọc và xuất báo cáo.

---

# 16. Kiểm thử

Project sử dụng xUnit.

## 16.1. Chạy toàn bộ test local

Integration test yêu cầu SQL Server riêng:

```powershell
$env:SHIFT_SCHEDULING_TEST_CONNECTION = `
"Server=(localdb)\MSSQLLocalDB;Database=ShiftSchedulingDb_IntegrationTest;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

Chạy:

```powershell
dotnet test src/backend/ShiftScheduling.sln `
  --configuration Release
```

Kết quả của bản phát hành cuối được xác nhận bằng GitHub Actions
trên Pull Request vào `main`.

Workflow CI thực hiện:

    Restore
      ↓
    Format / verify
      ↓
    Build Release
      ↓
    Khởi động SQL Server integration
      ↓
    Chạy toàn bộ test

Số lượng test có thể thay đổi khi bổ sung regression test,
vì vậy kết quả cuối cùng được đối chiếu trực tiếp trên workflow CI
của Pull Request phát hành.

---

# 17. Integration test với SQL Server

Database integration test sử dụng database riêng:

```text
ShiftSchedulingDb_IntegrationTest
```

Biến bắt buộc:

```text
SHIFT_SCHEDULING_TEST_CONNECTION
```

Fixture không có fallback sang database ứng dụng.

Fixture chỉ chấp nhận đúng database:

```text
ShiftSchedulingDb_IntegrationTest
```

Các database như:

```text
ShiftSchedulingDb
ShiftSchedulingDb_Demo
master
```

bị từ chối để tránh thao tác phá hủy nhầm dữ liệu.

Integration fixture hỗ trợ:

- Windows LocalDB;
- SQL Server qua TCP;
- SQL Server container trong CI.

Các test database hiện kiểm tra các luồng:

```text
IT-01  Tạo SchedulePeriod và StaffingRequirement
IT-02  Đăng ký Availability
IT-03  Leave Request và RowVersion
IT-04  Manual Shift Assignment
IT-05  NC-07 Workday Adjustment và Report
```

Integration tests tự tạo baseline test data và không phụ thuộc DemoDataSeeder.

---

# 18. Continuous Integration

Repository sử dụng GitHub Actions.

Workflow chạy trên:

```text
ubuntu-latest
```

CI khởi động SQL Server 2022 service và cung cấp:

```text
SHIFT_SCHEDULING_TEST_CONNECTION
```

cho bộ test.

Luồng CI:

```text
Checkout
   ↓
Setup .NET
   ↓
Restore
   ↓
Format / verify
   ↓
Build Release
   ↓
SQL Server sẵn sàng
   ↓
Chạy toàn bộ test
   ↓
Bao gồm 5 Controller Database Integration Tests
```

Database integration tests **không còn bị loại khỏi CI**.

CI sử dụng database:

```text
ShiftSchedulingDb_IntegrationTest
```

và không sử dụng database demo.

---

# 19. Tài liệu kiểm thử

Tài liệu kiểm thử nằm tại:

```text
docs/kiem-thu/
```

Các tài liệu chính gồm:

```text
testcase.md
matrantruyvet.md
BR_Test Coverage.md
```

Nội dung bao gồm:

- test case cho các chức năng chính;
- test case cho Business Rules;
- ma trận truy vết yêu cầu → test;
- coverage theo Business Rule;
- kết quả kiểm thử;
- regression test cho các defect đã phát hiện.

---

# 20. Khắc phục sự cố thường gặp

## Docker Web không chạy

Kiểm tra:

```powershell
docker compose ps
```

Xem log:

```powershell
docker compose logs --tail 150 web
```

---

## SQL Server container chưa healthy

```powershell
docker compose logs --tail 150 db
```

Kiểm tra giá trị:

```text
MSSQL_SA_PASSWORD
```

trong `.env`.

---

## Không kết nối được LocalDB

```powershell
sqllocaldb info MSSQLLocalDB
```

Nếu đã tồn tại:

```powershell
sqllocaldb start MSSQLLocalDB
```

---

## Seeder báo `Partial demo dataset detected`

Không sửa từng dòng dữ liệu bằng tay.

Với database demo Docker:

```powershell
docker compose stop web
```

```powershell
docker compose run --rm `
  -e DemoData__AllowReset=true `
  web `
  --reset-demo=ShiftSchedulingDb_Demo
```

```powershell
docker compose up -d web
```

Đây là cách reset chính thức cho database demo.

---

# 21. Triển khai

Hệ thống được triển khai trên **Azure Container Apps** và có thể truy cập qua HTTPS tại:

<https://shift-scheduling-web.braveisland-f86e5e3f.eastasia.azurecontainerapps.io/>

Kiến trúc triển khai:

    Docker image
        ↓
    Azure Container Registry
        ↓
    Azure Container Apps
        ↓
    Azure SQL Database

Các tài nguyên triển khai:

| Thành phần | Giá trị |
| --- | --- |
| Resource Group | `rg-shift-scheduling` |
| Container App | `shift-scheduling-web` |
| Container Registry | `cabb825dc9a2acr.azurecr.io` |
| Phiên bản bàn giao | `v1.0` |

Source code không thay đổi giữa môi trường local và production.
Connection string và thông tin nhạy cảm được cung cấp bằng biến môi trường
hoặc secret của Azure, không commit vào repository.

Schema trên môi trường triển khai được cập nhật bằng Entity Framework Core
Migrations, không chỉnh sửa trực tiếp bằng công cụ quản trị.

---

# 22. Repository

GitHub:

```text
https://github.com/binhnt1308/employee-shift-scheduling-system
```

---

# 23. Thông tin sinh viên

**Sinh viên thực hiện:** Nguyễn Thanh Bình  
**MSSV:** 2354050017  
**Giảng viên hướng dẫn:** Th.S Võ Việt Khoa