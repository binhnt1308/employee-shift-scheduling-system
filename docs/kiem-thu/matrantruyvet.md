## 1. Ma trận truy vết Functional Requirement (FR)

| Mã yêu cầu | Nội dung yêu cầu / Use Case | Testcase tài liệu phủ | Automated test / bằng chứng liên quan | Kết quả lần chạy gần nhất |
|---|---|---|---|---|
| **FR01** | **UC01 – Quản lý tài khoản / xác thực** | `TC-AUTH-001`, `TC-AUTH-002`, `TC-AUTH-003`, `TC-AUTH-004` | `TC01-01`, `TC01-02`, `TC01-03..10`, authorization tests; rate-limit source policy `login` | **3 Pass / 1 Blocked** — xUnit 22/08/2026, HTTP/UI cũ 15/08/2026 |
| **FR02** | **UC02 – Quản lý nhân viên** | `TC-EMP-001`, `TC-EMP-002`, `TC-EMP-004`, `TC-EMP-005` | `TC02-01..10`, `TC07-INT-02` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **FR03** | **UC03 – Thiết lập kỳ lịch và định mức ca** | `TC-PERIOD-001`, `TC-PERIOD-002`, `TC-PERIOD-004` | `TC03-01..06`, `TC03-A01..A03`, `TC03-B01..B06`, `TC03-C01..C03`, `TC03-D01..D02`, `TC03-08` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **FR04** | **UC04 – Quản lý lịch rảnh** | `TC-AVAIL-001`, `TC-AVAIL-002`, `TC-AVAIL-003` | `TC04-01..03` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **FR05** | **UC05 – Phân công và điều chỉnh lịch làm việc** | `TC-SCHEDULE-001`, `TC-SCHEDULE-002`, `TC-SCHEDULE-003`, `TC-SCHEDULE-004`, `TC-SCHEDULE-006`, `TC-SCHEDULE-007`, `TC-SCHEDULE-008`, `TC-SCHEDULE-009`, `TC-SCHEDULE-010`, `TC-SCHEDULE-011`, `TC-SCHEDULE-014`, `TC-SCHEDULE-018..021` | `TC05-01..05`, `TC-PT-01`, `TC-FT-01..08`, `TC05-INT-01`, BR20/BR21 tests | **Pass** — xUnit 22/08/2026; một số HTTP/UI cũ 15/08/2026 |
| **FR06** | **UC06 – Sinh lịch đề xuất tự động bằng OR-Tools** | `TC-ORTOOLS-001`, `TC-ORTOOLS-002`, `TC-ORTOOLS-003` | `TC06-01`, `TC06-02`, `TC06-03`, `TC06-INT-01`, `TC06-BR21-01` | **Pass** — xUnit 22/08/2026 |
| **FR07** | **UC07 – Duyệt và công bố lịch** | `TC-PUBLISH-003`, `TC-PUBLISH-004`, `TC-PUBLISH-005` | `TC07-INT-01`, `TC07-INT-02`, BR20/BR21 validation tests | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **FR08** | **UC08 – Xem lịch đã công bố** | `TC-MYSCHEDULE-001`, `TC-MYSCHEDULE-002`, `TC-MYSCHEDULE-003` | MySchedule / authorization regression tests trong suite hiện tại | **Pass (HTTP/UI cũ)** — 15/08/2026; **cần giữ 3 TC này trong testcase.md chính** |
| **FR09** | **UC09 – Quản lý đơn xin nghỉ** | `TC-LEAVE-001`, `TC-LEAVE-002`, `TC-LEAVE-005`, `TC-LEAVE-006`, `TC-LEAVE-007`, `TC-LEAVE-008`, `TC-LEAVE-009` | `TC09-01..10`, `DB-15 RowVersion OriginalValue`, `WorkScheduleLeaveReplacementTests` | **Pass** — xUnit 15/09/2026; HTTP/UI cũ 15/08/2026 |
| **FR10** | **UC10 – Quản lý yêu cầu đổi ca** | `TC-TRANSFER-001`, `TC-TRANSFER-003`, `TC-TRANSFER-004` | `TC10-01..10`, `TC10-R01..R11` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **FR11** | **UC11 – Tổng hợp số ca và giờ làm** | `TC-REPORT-001`, `TC-REPORT-002`, `TC-REPORT-004` | `TC11-01..03`, `TC-REPORT-CONSISTENCY-01..03` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |

---

## 2. Ma trận truy vết Business Rule (BR)

| Mã yêu cầu | Nội dung quy tắc nghiệp vụ | Testcase tài liệu phủ | Automated test / bằng chứng liên quan | Kết quả lần chạy gần nhất |
|---|---|---|---|---|
| **BR01** | Tài khoản bị khóa không đăng nhập; chức năng phải đúng quyền theo role. | `TC-AUTH-002`, `TC-AUTH-003` | `TC01-02`, authorization tests, `TC03-A03` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **BR02** | Chỉ nhân viên đang hoạt động mới được tham gia phân công; trạng thái Employee độc lập với trạng thái khóa UserAccount. | `TC-EMP-004`, `TC-EMP-005`, `TC-SCHEDULE-007` | `TC02-05..10`, `TC07-INT-02` | **Pass** — xUnit 22/08/2026 |
| **BR03** | Hệ thống dùng đúng 3 ca cố định; kỳ lịch 7 ngày tạo đúng 21 tổ hợp ngày-ca. | `TC-PERIOD-001`, `TC-PERIOD-004` | `TC03-01..06`, `TC03-B01..B06` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **BR04** | Availability chỉ được đăng ký khi kỳ đang Open và còn hạn đăng ký. | `TC-AVAIL-001`, `TC-AVAIL-002`, `TC-AVAIL-003` | `TC04-01..03` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **BR05** | Không phân công nhân viên vào ngày-ca mà nhân viên không đăng ký rảnh. | `TC-SCHEDULE-001` | `TC05-02`, `TC10-R01` | **Pass** — xUnit 22/08/2026 |
| **BR06** | Approved Leave chặn phân công vào ngày/ca nghỉ đã được duyệt. | `TC-SCHEDULE-002` | `TC05-03`, `TC10-R02` | **Pass** — xUnit 22/08/2026 |
| **BR07** | Nhân viên được phân công phải đúng Position của nhu cầu ca. | `TC-SCHEDULE-008` | `TC10-R03` | **Pass** — xUnit 22/08/2026; **TC-SCHEDULE-008 phải được giữ trong testcase.md** |
| **BR08** | Không cho phép assignment trùng/chồng ca của cùng nhân viên. | `TC-SCHEDULE-009` | `TC10-R04` | **Pass** — xUnit 22/08/2026; **TC-SCHEDULE-009 phải được giữ trong testcase.md** |
| **BR09** | Part-time tối đa 1 ca trong một ngày. | `TC-SCHEDULE-003` | `TC05-04`, `TC-PT-01`, `TC10-R05` | **Pass** — xUnit 22/08/2026 |
| **BR10** | Full-time phải làm đúng 2 ca liên tiếp trong ngày ở trạng thái lịch cuối: Morning+Afternoon hoặc Afternoon+Evening. | `TC-SCHEDULE-004`, `TC-SCHEDULE-014` | `TC05-05`, `TC-FT-02/03/04/05/06/07/08` | **Pass** — xUnit 22/08/2026 |
| **BR11** | Full-time tối đa 6 ngày làm việc trong kỳ 7 ngày. | `TC-SCHEDULE-010` | `TC10-R09` và regression BR11 hiện tại | **Pass** — xUnit 22/08/2026; **TC-SCHEDULE-010 phải được giữ trong testcase.md** |
| **BR12** | Mỗi ngày-ca phải đáp ứng định mức nhân sự theo Position trước khi Published. | `TC-SCHEDULE-005`, `TC-PUBLISH-003` | `TC05-01`, `TC03-C01..C03`, `TC10-R10` | **Pass** — xUnit 22/08/2026; **TC-SCHEDULE-005 phải được giữ trong testcase.md** |
| **BR13** | Mỗi ca có nhân sự phải đáp ứng yêu cầu nhân viên kinh nghiệm Level2+ theo cấu hình. | `TC-SCHEDULE-011`, `TC-PUBLISH-003` | `TC05-01`, `TC03-D01..D02`, `TC10-R07` | **Pass** — xUnit 22/08/2026; **TC-SCHEDULE-011 phải được giữ trong testcase.md** |
| **BR14** | Draft có thể được lưu/chỉnh sửa; chỉ Published mới là lịch chính thức để nhân viên xem và báo cáo sử dụng. | `TC-ORTOOLS-003`, `TC-PUBLISH-003`, `TC-MYSCHEDULE-002` | `TC06-03`, `TC06-INT-01`, Published-only report/MySchedule tests | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **BR15** | OR-Tools chỉ sinh Proposal; không tự động Published. | `TC-ORTOOLS-001`, `TC-ORTOOLS-002`, `TC-ORTOOLS-003` | `TC06-01..03`, `TC06-INT-01` | **Pass** — xUnit 22/08/2026 |
| **BR16** | Trước thao tác duyệt/công bố phải dùng trạng thái dữ liệu mới nhất; không xử lý lặp trạng thái đã thay đổi. | `TC-LEAVE-005`, `TC-PUBLISH-005` | `TC09-07`, `TC10-R08`, `TC07-INT-02` | **Pass** — xUnit 22/08/2026 |
| **BR17** | Đổi ca theo luồng Sender tạo → Receiver đồng ý → Manager duyệt; chỉ đổi một chiều khi toàn bộ điều kiện hợp lệ. | `TC-TRANSFER-001`, `TC-TRANSFER-003` | `TC10-01..10` | **Pass** — xUnit 22/08/2026; HTTP/UI cũ 15/08/2026 |
| **BR18** | Theo dõi workload và tổng hợp số ca/ngày/giờ; workload lệch là cảnh báo/đầu vào đánh giá, không tự biến thành hard reject nếu các hard rule vẫn đạt. | `TC-SCHEDULE-012`, `TC-REPORT-001` | `TC11-01`, workload warning regression | **Pass** — xUnit 22/08/2026; **TC-SCHEDULE-012 phải được giữ trong testcase.md** |
| **BR19** | OR-Tools ưu tiên cân bằng workload giữa các nhân viên cùng nhóm khi có nhiều phương án hợp lệ. | `TC-SCHEDULE-013` | fairness/optimization regression hiện tại | **Pass** — xUnit 22/08/2026; **TC-SCHEDULE-013 phải được giữ trong testcase.md** |
| **BR20 / BRD BR-03** | Có ít nhất 12 giờ nghỉ giữa ca cuối ngày D và ca đầu ngày D+1. | `TC-SCHEDULE-018`, `TC-SCHEDULE-020`, `TC-PUBLISH-004` | `TC-BR20-01..03`, `TC-LC04-01..03` | **Pass** — xUnit 22/08/2026; biên 12h và vi phạm 11h đều được kiểm |
| **BR21 / BRD BR-08** | Có ít nhất 24 giờ nghỉ liên tục trong mỗi kỳ 7 ngày. | `TC-SCHEDULE-019`, `TC-SCHEDULE-021`, `TC-PUBLISH-005`, `TC-TRANSFER-004` | `TC-BR21-01..04`, `TC06-BR21-01`, `TC05-INT-01`, `TC07-INT-01`, `TC10-R11` | **Pass** — xUnit 22/08/2026 |

---
