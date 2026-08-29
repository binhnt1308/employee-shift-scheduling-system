# Business Rule Test Coverage - Tuần 7

Mục tiêu: theo dõi mức độ bao phủ kiểm thử của các Business Rule hiện hành,
đồng thời truy vết từ Business Rule đến testcase tài liệu và automated test tương ứng.

### Quy ước hai hệ mã BR

Project hiện có mã BR nội bộ trong source/test và mã BRD gốc ở một số quy tắc không trùng số.

Để tránh nhầm, bảng dưới đây tách riêng hai cột:

| BR trong báo cáo | BRD tương ứng | Ý nghĩa |
|---|---|---|
| `BR20` | `BR-03` | Nghỉ tối thiểu 12 giờ giữa ca cuối ngày D và ca đầu ngày D+1 |
| `BR21` | `BR-08` | Có ít nhất 24 giờ nghỉ liên tục trong mỗi kỳ 7 ngày |

nếu cần đối chiếu toàn bộ BRD gốc thì thực hiện trong tài liệu
brd-mapping.md

## 1. Business Rule Test Coverage

| BR nội bộ | BRD tương ứng | Quy tắc nghiệp vụ hiện hành | Testcase tài liệu chính | Automated test tiêu biểu | Kỹ thuật / loại kiểm thử | Kết quả hiện tại |
|---|---|---|---|---|---|---|
| **BR01** | — | Tài khoản bị khóa không được đăng nhập; quyền truy cập được kiểm theo role. | `TC-AUTH-002`, `TC-AUTH-003` | `TC01-02`, `TC03-A03`, authorization tests | Phân hoạch tương đương; bảng quyết định role × endpoint | **Pass** |
| **BR02** | — | Chỉ Employee đang hoạt động được tham gia phân công; Employee status độc lập với UserAccount lock. | `TC-EMP-004`, `TC-EMP-005`, `TC-SCHEDULE-007` | `TC02-05..10`, `TC07-INT-02` | Bảng quyết định trạng thái; hồi quy lifecycle | **Pass** |
| **BR03** | — | Hệ thống sử dụng đúng 3 ca cố định; kỳ lịch 7 ngày tạo đúng 21 tổ hợp ngày-ca. | `TC-PERIOD-001`, `TC-PERIOD-004` | `TC03-01..06`, `TC03-B01..B06` | Phân hoạch tương đương; kiểm số lượng | **Pass** |
| **BR04** | — | Availability chỉ được đăng ký khi kỳ đang Open và còn deadline. | `TC-AVAIL-001`, `TC-AVAIL-002`, `TC-AVAIL-003` | `TC04-01..03` | Phân hoạch tương đương; phân tích biên deadline; chuyển trạng thái | **Pass** |
| **BR05** | — | Không phân công Employee vào ngày-ca mà Employee không đăng ký rảnh. | `TC-SCHEDULE-001` | `TC05-02`, `TC10-R01` | Phân hoạch tương đương – vi phạm | **Pass** |
| **BR06** | — | Approved Leave chặn phân công vào ngày/ca nghỉ đã duyệt. | `TC-SCHEDULE-002` | `TC05-03`, `TC10-R02` | Phân hoạch tương đương – vi phạm | **Pass** |
| **BR07** | — | Employee được phân công phải đúng Position của nhu cầu ca. | `TC-SCHEDULE-008` | `TC10-R03` | Phân hoạch tương đương – sai Position | **Pass** |
| **BR08** | — | Không cho phép assignment trùng/chồng ca của cùng Employee. | `TC-SCHEDULE-009` | `TC10-R04` | Phân hoạch tương đương – overlap/duplicate | **Pass** |
| **BR09** | — | Part-time tối đa 1 ca trong một ngày. | `TC-SCHEDULE-003` | `TC05-04`, `TC-PT-01`, `TC10-R05` | Phân tích ngưỡng số ca/ngày | **Pass** |
| **BR10** | — | Full-time ở trạng thái lịch cuối phải làm đúng 2 ca liên tiếp: Morning+Afternoon hoặc Afternoon+Evening. | `TC-SCHEDULE-004`, `TC-SCHEDULE-014` | `TC05-05`, `TC-FT-02/03/04/05/06/07/08` | Phân hoạch tương đương; bảng quyết định cặp ca | **Pass** |
| **BR11** | — | Full-time tối đa 6 ngày làm việc trong kỳ 7 ngày. | `TC-SCHEDULE-010` | `TC10-R09` và regression BR11 hiện tại | Phân tích giá trị biên số ngày | **Pass** |
| **BR12** | — | Mỗi ngày-ca phải đáp ứng định mức nhân sự theo Position trước khi Published. | `TC-SCHEDULE-005`, `TC-PUBLISH-003` | `TC05-01`, `TC03-C01..C03`, `TC10-R10` | Phân hoạch tương đương; validation trước Publish | **Pass** |
| **BR13** | — | Ca có nhân sự phải đáp ứng yêu cầu nhân viên kinh nghiệm Level2+ theo cấu hình. | `TC-SCHEDULE-011`, `TC-PUBLISH-003` | `TC05-01`, `TC03-D01..D02`, `TC10-R07` | Phân tích biên Level; validation trước Publish | **Pass** |
| **BR14** | — | Draft có thể lưu/chỉnh sửa; chỉ Published mới là lịch chính thức để xem và báo cáo. | `TC-ORTOOLS-003`, `TC-PUBLISH-003`, `TC-MYSCHEDULE-002` | `TC06-03`, `TC06-INT-01`, Published-only regression | Chuyển trạng thái Draft → Published; hồi quy | **Pass** |
| **BR15** | — | OR-Tools chỉ sinh Proposal, không tự động Published. | `TC-ORTOOLS-001`, `TC-ORTOOLS-002`, `TC-ORTOOLS-003` | `TC06-01`, `TC06-02`, `TC06-03`, `TC06-INT-01` | Phân hoạch tương đương khả thi/không khả thi; hồi quy | **Pass** |
| **BR16** | — | Trước thao tác duyệt/công bố phải dùng trạng thái dữ liệu mới nhất; không xử lý lặp trạng thái đã thay đổi. | `TC-LEAVE-005`, `TC-PUBLISH-005` | `TC09-07`, `TC10-R08`, `TC07-INT-02` | Chuyển trạng thái; revalidation dữ liệu mới nhất | **Pass** |
| **BR17** | — | Đổi ca theo luồng Sender tạo → Receiver đồng ý → Manager duyệt; chỉ đổi một chiều khi hợp lệ. | `TC-TRANSFER-001`, `TC-TRANSFER-003` | `TC10-01..10` | Chuyển trạng thái; bảng quyết định | **Pass** |
| **BR18** | — | Theo dõi workload và tổng hợp số ca/ngày/giờ; lệch workload là warning, không tự thành hard reject. | `TC-SCHEDULE-012`, `TC-REPORT-001` | `TC11-01`, workload warning regression | Hồi quy warning; kiểm báo cáo | **Pass** |
| **BR19** | — | OR-Tools ưu tiên cân bằng workload giữa các Employee cùng nhóm khi có nhiều phương án hợp lệ. | `TC-SCHEDULE-013` | fairness/optimization regression hiện tại | Kiểm mục tiêu tối ưu / hồi quy | **Pass** |
| **BR20** | **BR-03** | Có ít nhất 12 giờ nghỉ giữa ca cuối ngày D và ca đầu ngày D+1. | `TC-SCHEDULE-018`, `TC-SCHEDULE-020`, `TC-PUBLISH-004` | `TC-BR20-01..03`, `TC-LC04-01..03` | **Phân tích giá trị biên:** 11h, 12h, >12h; biên ngày/tháng/năm | **Pass** |
| **BR21** | **BR-08** | Có ít nhất 24 giờ nghỉ liên tục trong mỗi kỳ 7 ngày. | `TC-SCHEDULE-019`, `TC-SCHEDULE-021`, `TC-PUBLISH-005`, `TC-TRANSFER-004` | `TC-BR21-01..04`, `TC06-BR21-01`, `TC05-INT-01`, `TC07-INT-01`, `TC10-R11` | **Phân tích giá trị biên:** đúng 24h / <24h; integration Publish/Transfer/OR-Tools | **Pass** |

## 2. Kiểm tra mức phủ

| Nội dung | Kết quả |
|---|---:|
| Tổng Business Rule nội bộ hiện hành | **21** |
| BR có testcase tài liệu phủ | **21** |
| BR có automated test / regression liên quan | **21** |
| BR chưa có coverage | **0** |


## 3. Testcase bắt buộc phải tồn tại trong `testcase.md`

Để bảng coverage này hợp lệ, các testcase sau không được xóa khỏi tài liệu testcase chính:

- `TC-SCHEDULE-005` — BR12.
- `TC-SCHEDULE-007` — BR02.
- `TC-SCHEDULE-008` — BR07.
- `TC-SCHEDULE-009` — BR08.
- `TC-SCHEDULE-010` — BR11.
- `TC-SCHEDULE-011` — BR13.
- `TC-SCHEDULE-012` — BR18.
- `TC-SCHEDULE-013` — BR19.
- `TC-SCHEDULE-018`, `TC-SCHEDULE-020` — BR20.
- `TC-SCHEDULE-019`, `TC-SCHEDULE-021` — BR21.
- `TC-MYSCHEDULE-002` — BR14.

Nếu một testcase tài liệu bị xóa hoặc thay đổi ý nghĩa, dòng BR tương ứng phải được cập nhật ;
không được chỉ dựa vào tên automated test để tuyên bố BR đã được phủ.

## 4. Quan hệ với ma trận truy vết

- File này trả lời: **mỗi BR đã được kiểm bằng testcase nào và test tự động nào?**
- `matrantruyvet.md` trả lời rộng hơn: **mỗi FR/BR trong SRS được testcase nào phủ và kết quả lần chạy gần nhất là gì?**
