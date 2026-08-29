# TEST CASES — EMPLOYEE SHIFT SCHEDULING SYSTEM

## 1. Phạm vi và quy ước

Tài liệu này là **bộ testcase chính** dùng ở Tuần 7. Các testcase chi tiết lặp lại ở mức form/validation
không đưa hết vào bảng chính để tài liệu dễ đọc; phần lõi ưu tiên Authentication, Employee lifecycle,
SchedulePeriod, Availability, lập lịch thủ công, OR-Tools, Publish, Leave Request, Shift Transfer và Report.

**Mốc source hiện tại**

- Branch: `tuan-07`
- HEAD: `058317a`
- Automated test: **140/140 PASS, 0 Failed, 0 Skipped**
- Lượt HTTP/UI gần nhất còn lưu trong tài liệu: `15/08/2026`, commit `779e73d`
- ### Quy ước mã testcase

Mã testcase tài liệu giữ chuẩn `TC-<MODULE>-<NNN>` theo TC-02. Mã đã cấp không đổi và không tái sử dụng.
Một số automated test trong source được tạo ở các giai đoạn trước nên còn dùng mã như `TC01-01`,
`TC05-INT-01`, `TC-BR20-03`, `TC10-R11`... Bảng **Truy vết testcase → source/test tự động** ở cuối
tài liệu ánh xạ hai hệ mã này.

Mỗi testcase giữ đúng 9 trường theo TC-03:

`Mã TC` · `Chức năng / UC` · `Mục tiêu` · `Tiền điều kiện` · `Dữ liệu đầu vào` ·
`Các bước thực hiện` · `Kết quả mong đợi` · `Kết quả thực tế` · `Trạng thái`.

- `Pass`: Actual khớp Expected.
- `Fail`: Actual khác Expected; bắt buộc có GitHub Issue tương ứng.
- `Blocked`: chưa thể kết luận do môi trường/contract/phụ thuộc chưa chốt.
- Testcase âm bị hệ thống chặn đúng vẫn là **Pass**.
- Expected phải đo được: HTTP status, `Location`, JSON, message cụ thể hoặc trạng thái record.
- Với POST có AntiForgery, phải dùng token hợp lệ và giữ cookie phiên.
- Không tự động follow redirect khi cần ghi raw HTTP status.
- ## 2. Kỹ thuật kiểm thử em sử dụng

| Kỹ thuật | Cách áp dụng | Testcase tiêu biểu |
|---|---|---|
| Phân hoạch tương đương | Chia lớp hợp lệ/không hợp lệ có cùng hành vi. | `TC-AUTH-001/002`, `TC-EMP-001/002`, `TC-SCHEDULE-001/002/004` |
| Phân tích giá trị biên | Kiểm ngay dưới, đúng và trên ngưỡng. | `TC-SCHEDULE-018`, `TC-SCHEDULE-020`, `TC-SCHEDULE-019`, `TC-SCHEDULE-021` |
| Bảng quyết định | Kết quả phụ thuộc tổ hợp role/trạng thái/dữ liệu. | `TC-AUTH-003`, `TC-PUBLISH-002`, `TC-TRANSFER-003/004` |
| Chuyển trạng thái | Kiểm vòng đời entity. | Draft → Published; Pending → Approved; WaitingReceiver → WaitingManager → Approved |
| Hồi quy | Khóa lại lỗi/phần đã sửa để tránh tái phát. | `TC-ORTOOLS-003`, `TC-REPORT-004`, BR20, BR21, Employee lifecycle |
## 3. Testcase chính

| Mã TC | Chức năng / UC | Mục tiêu | Tiền điều kiện | Dữ liệu đầu vào | Các bước thực hiện | Kết quả mong đợi | Kết quả thực tế | Trạng thái |
|---|---|---|---|---|---|---|---|---|
| **TC-AUTH-001** | UC01 – Đăng nhập | **Phân hoạch tương đương – hợp lệ.** Manager đăng nhập và tạo phiên đúng role. | `manager_demo` tồn tại, đúng mật khẩu, `IsLocked=false`, `ReturnUrl` rỗng. | POST `/Account/Login`; Username=`manager_demo`; Password=`Demo@123`; AntiForgery hợp lệ. | 1. GET Login lấy token. 2. POST credential. 3. Ghi status/Location/cookie. 4. GET route sau redirect. | **HTTP 302** về Home (`/` hoặc `/Home/Index`); auth cookie được đặt; GET Home **HTTP 200**; principal có role Manager. | HTTP 302; `Location=/`; auth cookie được đặt; GET `/` HTTP 200, menu Manager đúng. | **Pass — 15/08/2026 — 779e73d** |
| **TC-AUTH-002** | UC01 / BR01 – Account locked | **Phân hoạch tương đương – không hợp lệ.** Account khóa không tạo phiên dù mật khẩu đúng. | Account tồn tại; `IsLocked=true`; password đúng. | POST `/Account/Login` với credential đúng. | 1. GET token. 2. POST Login. 3. Ghi HTTP/cookie/message. | **HTTP 200**; render lại Login; không đặt auth cookie; hiện đúng `Tên đăng nhập hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.` | HTTP 200; không đặt cookie; đúng thông báo. | **Pass — 15/08/2026 — 779e73d** |
| **TC-AUTH-003** | UC01 / BR01 / TC-12 – Sai role | **Bảng quyết định role × endpoint.** Employee không vào SchedulePeriod Manager-only. | Đã đăng nhập Employee. | GET `/SchedulePeriod`. | 1. Login Employee. 2. Gọi trực tiếp URL. 3. Ghi status/Location. | **HTTP 302** tới `/Account/AccessDenied?ReturnUrl=%2FSchedulePeriod`; không lộ dữ liệu kỳ lịch. | HTTP 302; redirect AccessDenied; không lộ dữ liệu. | **Pass — 15/08/2026 — 779e73d** |
| **TC-AUTH-004** | UC01 / SE-11 – Rate limit Login | **Phân tích ngưỡng.** Login bị giới hạn sau số request cho phép. | Chưa đăng nhập; cùng IP; policy `login` đang bật. | Gửi >10 POST Login trong 1 phút. | 1. Lấy token hợp lệ. 2. Gửi liên tiếp request Login. 3. Ghi response từng lần. | Khi vượt quota: **HTTP 429 Too Many Requests**; có `Retry-After`; body `Bạn đã thử đăng nhập quá nhiều lần. Vui lòng chờ một lát rồi thử lại.` | Chưa có lượt HTTP/UI mới được lưu sau commit thêm rate limit; source đã gắn `[EnableRateLimiting("login")]`. | **Blocked — 22/08/2026 — 058317a** |
| **TC-EMP-001** | UC02 – Tạo nhân viên | **Phân hoạch tương đương – hợp lệ.** Tạo Employee hợp lệ. | Admin/Manager; mã/email chưa tồn tại. | POST `/Employee/Create`; `EMP901`; email giả hợp lệ; Position=Sales; PartTime; Level2. | 1. Mở Create. 2. Nhập dữ liệu. 3. POST. 4. Ghi HTTP/Location. 5. Kiểm DB. | **HTTP 302** về `/Employee`; DB tạo đúng 1 Employee `EMP901`; `IsActive=true`. | HTTP 302; DB tạo đúng EMP901, `IsActive=1`. | **Pass — 15/08/2026 — 779e73d** |
| **TC-EMP-002** | UC02 – Duplicate EmployeeCode | **Phân hoạch tương đương – không hợp lệ.** Chặn mã nhân viên trùng. | Đã có `EMP900`. | POST Create với `EmployeeCode=EMP900`, email khác. | 1. Nhập mã trùng. 2. POST. 3. Ghi HTTP/message. 4. Đếm DB. | **HTTP 200**; render lại form; hiện `Mã nhân viên đã tồn tại.`; DB vẫn chỉ có 1 `EMP900`. | HTTP 200; đúng thông báo; DB không thêm record. | **Pass — 15/08/2026 — 779e73d** |
| **TC-EMP-004** | UC02 – Deactivate Employee | **Bảng quyết định trạng thái lịch.** Không deactivate Employee còn Published assignment hôm nay/tương lai. | Employee Active; có assignment trong WorkSchedule `Published`, `WorkDate >= hôm nay`. | POST đổi `IsActive=false`. | 1. Mở Edit. 2. Chọn ngừng hoạt động. 3. POST. 4. Kiểm DB. | Service từ chối; `Employee.IsActive` vẫn `true`; Published assignment không bị xóa/sửa. | Automated regression `TC02-07`, `TC02-08` PASS trên HEAD hiện tại. | **Pass — 22/08/2026 — 058317a** |
| **TC-EMP-005** | UC02 – Reactivate Employee | **Hồi quy.** Reactivate Employee không tự mở khóa account. | Employee `IsActive=false`; UserAccount liên kết `IsLocked=true`. | POST đổi `IsActive=true`. | 1. Reactivate Employee. 2. Đọc lại Employee và UserAccount. | `Employee.IsActive=true`; `UserAccount.IsLocked` vẫn `true`; hai trạng thái độc lập. | `TC02-05`, `TC02-06` PASS trên HEAD hiện tại. | **Pass — 22/08/2026 — 058317a** |
| **TC-PERIOD-001** | UC03 / BR03 – Tạo kỳ lịch | **Chuyển trạng thái + kiểm số lượng.** Tạo 7 ngày × 3 ca = 21 requirement. | Manager; có đúng fixed MORNING/AFTERNOON/EVENING; tuần chưa tồn tại. | POST Create; WeekStartDate hợp lệ; deadline tương lai. | 1. Mở Create. 2. Nhập. 3. POST. 4. Kiểm HTTP/TempData. 5. Kiểm DB. | **HTTP 302** về `/SchedulePeriod`; TempData `Khởi tạo kỳ lịch 7 ngày và 21 định mức ca thành công.`; đúng **21 StaffingRequirement**. | HTTP 302; đúng TempData; DB có 21 rows. | **Pass — 15/08/2026 — 779e73d** |
| **TC-PERIOD-002** | UC03 – Trùng kỳ | **Phân hoạch tương đương – conflict.** Không tạo period trùng tuần. | Đã có period cùng tuần. | POST Create với WeekStartDate trùng. | 1. POST period trùng. 2. Ghi HTTP/message. 3. Kiểm DB. | **HTTP 200**; hiện `Kỳ lịch trùng thời gian với kỳ lịch đã có.`; không tạo period/requirements mới. | HTTP 200; đúng thông báo; DB không tăng. | **Pass — 15/08/2026 — 779e73d** |
| **TC-PERIOD-004** | UC03 / BR03 – Fixed shifts sai/thiếu | **Phân hoạch tương đương – cấu hình không hợp lệ.** Không tạo period nếu thiếu/sai 3 ca chuẩn. | Thiếu MORNING/AFTERNOON/EVENING hoặc một ca sai giờ. | POST Create với period hợp lệ. | 1. Chuẩn bị fixed-shift sai. 2. POST. 3. Ghi lỗi. 4. Kiểm DB. | **HTTP 200**; business error `ShiftTypeNotFound`/cấu hình ca; không tạo period và không tạo 21 rows. | HTTP 200; lỗi cấu hình ca; DB không tạo period. | **Pass — 15/08/2026 — 779e73d** |
| **TC-AVAIL-001** | UC04 / BR04 – Lưu lịch rảnh | **Phân hoạch tương đương – hợp lệ.** Lưu Availability khi period Open và còn hạn. | Employee; period Open; deadline chưa hết. | Chọn 2 ngày-ca hợp lệ. | 1. Mở Register. 2. Chọn 2 ca. 3. POST Save. 4. Mở lại. 5. Kiểm DB. | **HTTP 302** về Availability; TempData `Đã lưu lịch rảnh thành công.`; DB có đúng 2 record. | HTTP 302; đúng TempData; DB có 2 record. | **Pass — 15/08/2026 — 779e73d** |
| **TC-AVAIL-002** | UC04 / BR04 – Hết deadline | **Phân tích biên deadline.** Không cập nhật Availability sau hạn. | Period Open nhưng deadline đã qua. | POST thêm một ca. | 1. Sửa lựa chọn. 2. POST. 3. Ghi HTTP/TempData. 4. Kiểm DB. | **HTTP 302** về `/Availability/Register/{id}`; TempData `Đã hết thời hạn đăng ký lịch rảnh.`; DB giữ nguyên. | HTTP 302; đúng TempData; DB giữ nguyên. | **Pass — 15/08/2026 — 779e73d** |
| **TC-AVAIL-003** | UC04 / BR04 – Period Closed | **Chuyển trạng thái.** Không đăng ký Availability khi period không Open. | Period `Closed`. | POST selection hợp lệ. | 1. POST. 2. Ghi HTTP/TempData. 3. Kiểm DB. | **HTTP 302** về Register; TempData `Kỳ lịch hiện không mở đăng ký lịch rảnh.`; DB không đổi. | HTTP 302; đúng TempData; DB không đổi. | **Pass — 15/08/2026 — 779e73d** |
| **TC-SCHEDULE-006** | UC05 – Manual assign hợp lệ | **Phân hoạch tương đương – hợp lệ.** Phân công thỏa hard rules được lưu vào Draft. | Manager; Employee Active, đúng Position, có Availability, không Approved Leave, không vi phạm workload/rest rules. | POST `/WorkSchedule/AssignManual` với ngày-ca hợp lệ. | 1. GET Edit lấy token. 2. POST AssignManual. 3. Ghi HTTP/JSON. 4. Kiểm DB. | Theo source HEAD: **HTTP 200 + JSON `success=true`**; tạo 1 ShiftAssignment; WorkSchedule vẫn Draft. | HTTP 200 + success=true ở lượt HTTP cũ; các regression liên quan vẫn xanh trên HEAD. | **Pass — HTTP 15/08/2026; xUnit 22/08/2026** |
| **TC-SCHEDULE-001** | UC05 / BR05 – Ngoài Availability | **Phân hoạch tương đương – vi phạm.** Chặn Employee chưa đăng ký rảnh. | Employee Active nhưng không có Availability đúng ngày-ca. | POST AssignManual. | 1. Chọn Employee không rảnh. 2. POST. 3. Ghi JSON. 4. Kiểm DB. | Theo controller HEAD: **HTTP 200 + JSON `success=false`**; message BR05/không đăng ký rảnh; không tạo ShiftAssignment. | Business rule được automated test `TC05-02` chứng minh bị chặn; HTTP cần chạy lại nếu muốn cập nhật raw status của HEAD. | **Pass (business rule) — 22/08/2026 — 058317a** |
| **TC-SCHEDULE-002** | UC05 / BR06 – Approved Leave | **Phân hoạch tương đương – vi phạm.** Không phân công người có đơn nghỉ Approved. | LeaveRequest Approved đúng ngày/ca. | POST AssignManual. | 1. POST Employee đã nghỉ. 2. Ghi JSON. 3. Kiểm DB. | Theo controller HEAD: **HTTP 200 + JSON `success=false`**; message BR06; không tạo assignment. | `TC05-03` PASS; current service lấy Approved Leave trước validation. | **Pass (business rule) — 22/08/2026 — 058317a** |
| **TC-SCHEDULE-003** | UC05 / BR09 – PT ca thứ hai | **Phân hoạch tương đương – vi phạm.** PT tối đa 1 ca/ngày. | PT đã có MORNING. | POST thêm AFTERNOON cùng ngày. | 1. POST ca thứ hai. 2. Ghi JSON. 3. Kiểm DB. | Theo controller HEAD: **HTTP 200 + JSON `success=false`**; lỗi BR09; vẫn chỉ 1 assignment. | `TC05-04`, `TC-PT-01` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-SCHEDULE-004** | UC05 / BR10 – FT Morning + Evening | **Phân hoạch tương đương – vi phạm.** Chặn cặp FT không liên tiếp. | FT đã có MORNING. | POST thêm EVENING cùng ngày. | 1. POST EVENING. 2. Ghi JSON. 3. Kiểm DB. | Theo controller HEAD: **HTTP 200 + JSON `success=false`**; lỗi BR10; EVENING không được thêm. | `TC05-05`, `TC-FT-05` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-SCHEDULE-014** | UC05 / BR10 – FT Morning + Afternoon | **Phân hoạch tương đương – hợp lệ.** Cặp FT liên tiếp được chấp nhận. | FT đã có MORNING; các rule khác hợp lệ. | POST thêm AFTERNOON. | 1. POST. 2. Kiểm kết quả. 3. Kiểm assignments. | **HTTP 200 + JSON `success=true`**; cuối ngày có đúng MORNING+AFTERNOON; validation BR10 không lỗi. | `TC-FT-02`, `TC-FT-07` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-SCHEDULE-018** | BR20 – Đúng 12 giờ nghỉ | **Phân tích giá trị biên – đúng biên.** 12 giờ được chấp nhận. | Ca cuối D kết thúc 20:00; ca đầu D+1 bắt đầu 08:00. | Gap = **12 giờ**. | 1. Chuẩn bị cặp assignment. 2. Chạy rest validation. 3. Kiểm errors. | `SchedulingRestRuleEvaluator` không sinh lỗi BR20; lịch hợp lệ theo ngưỡng 12 giờ. | `TC-BR20-01`, `TC-LC04-01/02/03` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-SCHEDULE-020** | BR20 – Nghỉ 11 giờ | **Phân tích giá trị biên – dưới biên.** 11 giờ phải bị chặn. | Synthetic shift D kết thúc 21:00; D+1 bắt đầu 08:00. | Gap = **11 giờ**. | 1. Chuẩn bị dữ liệu. 2. Chạy rest validation/manual assignment. 3. Kiểm lỗi và DB. | Validation sinh lỗi BR20; assignment/lịch vi phạm không được chấp nhận. Nếu đi qua `AssignManual` HEAD hiện trả JSON failure. | `TC-BR20-03` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-SCHEDULE-019** | BR21 – Đúng 24 giờ nghỉ | **Phân tích giá trị biên – đúng biên.** Có đúng 24h nghỉ liên tục là hợp lệ. | Lịch 7 ngày có một gap chính xác 24h. | Gap = **24 giờ**. | 1. Chuẩn bị assignments. 2. Chạy validation. 3. Kiểm errors. | Không có lỗi BR21; lịch thỏa yêu cầu ≥24h liên tục. | `TC-BR21-01` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-SCHEDULE-021** | BR21 – Không đủ 24 giờ | **Phân tích giá trị biên / equivalence – vi phạm.** Lịch không có gap ≥24h phải bị chặn. | PT MORNING cả 7 ngày hoặc FT làm 7 ngày. | Max gap <24h. | 1. Chuẩn bị lịch. 2. Chạy validation. 3. Kiểm errors/status. | Validation sinh lỗi BR21; lịch không đủ điều kiện final/Publish. | `TC-BR21-02`, `TC-BR21-03`, `TC05-INT-01` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-ORTOOLS-001** | UC06 / BR15 – Proposal khả thi | **Phân hoạch tương đương – khả thi.** Solver sinh proposal khi có nghiệm. | Period có đủ dữ liệu và tồn tại nghiệm thỏa hard rules. | POST `/WorkSchedule/GenerateOrToolsProposal/{id}`. | 1. POST Generate. 2. Ghi JSON. 3. Hậu kiểm proposal. | **HTTP 200 + JSON**; solver `Optimal` hoặc `Feasible`; proposal không vi phạm hard rules; không auto-Published. | `TC06-01` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-ORTOOLS-002** | UC06 / BR15 – Infeasible | **Phân hoạch tương đương – không khả thi.** Không tạo lịch sai khi không có nghiệm. | Không có Employee khả thi/constraints xung đột. | POST Generate. | 1. Chuẩn bị infeasible. 2. POST Generate. 3. Kiểm JSON/DB. | **HTTP 200 + JSON** trạng thái `INFEASIBLE`/no-solution; không auto-Publish; không lưu assignments vi phạm. | `TC06-02` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-ORTOOLS-003** | UC06 / BR15 – Proposal only | **Hồi quy / chuyển trạng thái.** Generate chỉ sinh đề xuất. | Solver có nghiệm. | POST Generate. | 1. Ghi status trước. 2. Generate. 3. Kiểm status/PublishedAt. | **HTTP 200 + JSON** có proposal; WorkSchedule không thành Published; `PublishedAt=null`; chỉ Adopt mới ghi proposal vào Draft. | `TC06-03`, `TC06-INT-01` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-PUBLISH-003** | UC07 – Publish hợp lệ | **Chuyển trạng thái Draft → Published.** Lịch hợp lệ được công bố. | Draft thỏa toàn bộ hard rules. | POST `/WorkSchedule/Publish/{id}`. | 1. Validate Draft. 2. POST Publish. 3. Ghi redirect/TempData. 4. Kiểm DB. | **HTTP 302** về Edit; TempData `Đã duyệt và công bố chính thức lịch làm việc cho kỳ lịch.`; `Status=Published`; `PublishedAt` có giá trị. | HTTP 302; đúng TempData; Status Published trong lượt HTTP đã lưu. | **Pass — 15/08/2026 — 779e73d** |
| **TC-PUBLISH-004** | UC07 / BR20 – Publish vi phạm 12h | **Bảng quyết định hard-rule × Publish.** Chặn lịch có gap <12h. | Draft chứa BR20 violation. | POST Publish. | 1. Chuẩn bị Draft lỗi. 2. POST Publish. 3. Kiểm redirect/TempData/status. | **HTTP 302** về Edit; TempData Error chứa BR20; `Status=Draft`; `PublishedAt=null`. | `TC-BR20-03` + Publish validation hiện còn trong source; HTTP cũ PASS. | **Pass — 15/08/2026 — 779e73d** |
| **TC-PUBLISH-005** | UC07 / BR21 – Publish thiếu 24h | **Bảng quyết định hard-rule × Publish.** Chặn Draft vi phạm BR21. | Draft không có khoảng nghỉ liên tục ≥24h. | POST Publish. | 1. Chuẩn bị Draft lỗi. 2. POST Publish. 3. Kiểm status. | **HTTP 302** về Edit; TempData Error chứa BR21; `Status=Draft`; `PublishedAt=null`. | `TC07-INT-01` PASS trên HEAD. | **Pass — 22/08/2026 — 058317a** |
| **TC-LEAVE-001** | UC09 – Gửi đơn nghỉ | **Chuyển trạng thái.** Đơn hợp lệ tạo ở Pending. | Employee; date/shift thuộc period, chưa kết thúc, không duplicate. | POST `/LeaveRequest/Create`; Reason hợp lệ. | 1. Mở Create. 2. Nhập. 3. POST. 4. Kiểm DB/TempData. | **HTTP 302** về LeaveRequest; TempData `Đã gửi đơn xin nghỉ. Đơn đang chờ Quản lý xử lý.`; tạo đúng 1 `Pending`. | `TC09-01` PASS; HTTP cũ đúng 302. | **Pass — xUnit 22/08/2026; HTTP 15/08/2026** |
| **TC-LEAVE-002** | UC09 – Ngày ngoài kỳ | **Phân hoạch tương đương – vi phạm.** Chặn LeaveDate ngoài SchedulePeriod. | Period xác định 7 ngày. | POST Create với date ngoài period. | 1. Nhập date ngoài kỳ. 2. POST. 3. Kiểm message/DB. | **HTTP 200**; render lại form; `Ngày xin nghỉ phải thuộc kỳ lịch đã chọn.`; không tạo request. | `TC09-02` PASS; HTTP cũ đúng 200. | **Pass — xUnit 22/08/2026; HTTP 15/08/2026** |
| **TC-LEAVE-005** | UC09 / BR16 – Manager Approve | **Chuyển trạng thái Pending → Approved.** Review dùng dữ liệu mới nhất. | Manager; LeaveRequest đang Pending; RowVersion hợp lệ. | POST `/LeaveRequest/Review/{id}` Approve. | 1. Mở Review. 2. POST Approve. 3. Kiểm status/audit/notification. | **HTTP 302** về LeaveRequest; request `Approved`; reviewer/time được cập nhật; tạo notification cho Employee. | `TC09-06`, `TC09-07` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-TRANSFER-001** | UC10 / BR17 – Đổi ca hợp lệ | **Chuyển trạng thái.** Sender Create → Receiver Accept → Manager Approve. | Published assignment; sender sở hữu ca; receiver hợp lệ. | POST Create → Respond accept → Review approve. | 1. Sender Create. 2. Receiver Accept. 3. Manager Approve. 4. Kiểm request/assignment. | Mỗi POST theo MVC redirect **HTTP 302**; cuối flow request Approved; cùng ShiftAssignment đổi EmployeeId sang receiver; không tạo ca ngược. | `TC10-01`, `TC10-04`, `TC10-08` PASS; HTTP cũ đúng flow. | **Pass — 22/08/2026 — 058317a** |
| **TC-TRANSFER-003** | UC10 / BR17 – Approve quá sớm | **Bảng quyết định receiver-status × manager-action.** Không approve trước khi receiver đồng ý. | ReceiverStatus Pending. | POST Review `approve=true`. | 1. Manager POST Review. 2. Ghi TempData. 3. Kiểm request/assignment. | **HTTP 302** về ShiftTransfer; TempData `Người nhận chưa đồng ý nhận ca.`; ApprovalStatus không Approved; assignment không đổi. | `TC10-07` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-TRANSFER-004** | UC10 / BR21 – Receiver vi phạm nghỉ | **Bảng quyết định + rest-rule.** Không cho transfer phá BR21. | Receiver nhận ca sẽ mất khoảng nghỉ ≥24h. | Respond/Review theo flow hợp lệ đến bước revalidate. | 1. Chuẩn bị receiver schedule. 2. Accept. 3. Manager Approve. 4. Kiểm lỗi/assignment. | Request không được hoàn tất; thông báo chứa BR21/nội dung nghỉ 24h; assignment vẫn sender. | `TC10-R11` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-REPORT-001** | UC11 – Tổng hợp lịch Published | **Phân hoạch tương đương.** Báo cáo chỉ tính lịch chính thức. | Có Published assignments xác định. | GET Report theo period. | 1. Mở report. 2. Ghi HTTP. 3. Đối chiếu Shift/Day/Hours. | **HTTP 200**; tổng số ca, số ngày, tổng giờ đúng dữ liệu Published; Draft/Proposal không được tính. | `TC11-01` PASS; HTTP cũ PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-REPORT-002** | UC11 – CSV | **Phân hoạch tương đương – export.** CSV có header và dòng dữ liệu đúng. | Có dữ liệu Report. | Thực hiện Export CSV. | 1. Mở Report. 2. Export. 3. Đọc file. | Response tải file thành công; CSV có đúng header và row dữ liệu theo report. | `TC11-02` PASS. | **Pass — 22/08/2026 — 058317a** |
| **TC-REPORT-004** | UC11 – DurationHours | **Hồi quy.** Không hard-code `ShiftCount × 4`. | 1 Published assignment có `ShiftType.DurationHours=6`. | GET Report. | 1. Mở Report. 2. Kiểm TotalShifts/Days/Hours. | **HTTP 200**; `TotalShifts=1`; `TotalDays=1`; `TotalHours=6`; không trả 4. | `TC11-03`, `TC-REPORT-CONSISTENCY-*` PASS. | **Pass — 22/08/2026 — 058317a** |
## 4. Truy vết testcase → automated test → source

> Toàn bộ automated tests dưới đây thuộc bộ **140/140 PASS** trên HEAD `058317a`.

| Mã TC tài liệu | Test trong code | UC / BR | Source hiện thực chính |
|---|---|---|---|
| `TC-AUTH-001` | `TC01-01` | UC01 | `AuthenticationController`, `AuthenticationService` |
| `TC-AUTH-002` | `TC01-02` | UC01 / BR01 | `AuthenticationService.AuthenticateAsync` |
| `TC-AUTH-003` | `TC03-A03` | BR01 / TC-12 | `[Authorize]`, `SchedulePeriodController` |
| `TC-AUTH-004` | — manual security case | SE-11 | `AuthenticationController` `[EnableRateLimiting("login")]`, policy `login` |
| `TC-EMP-001` | `TC02-01` | UC02 / BR02 | `EmployeeService` |
| `TC-EMP-002` | `TC02-02` | UC02 | `EmployeeService` / Employee repository |
| `TC-EMP-004` | `TC02-07`, `TC02-08` | Employee lifecycle | Employee status service + Published-assignment query |
| `TC-EMP-005` | `TC02-05`, `TC02-06` | Employee/account independence | Employee status service + UserAccount state |
| `TC-PERIOD-001` | `TC03-B01` | UC03 / BR03 | `SchedulePeriodService`, fixed ShiftType constants |
| `TC-PERIOD-002` | `TC03-08` | UC03 | `SchedulePeriodService` |
| `TC-PERIOD-004` | `TC03-B03`, `TC03-B04` | BR03 | SchedulePeriod fixed-shift validation |
| `TC-AVAIL-001` | `TC04-01` | UC04 / BR04 | Availability service/repository |
| `TC-AVAIL-002` | `TC04-02` | BR04 | Availability deadline validation |
| `TC-AVAIL-003` | `TC04-03` | BR04 | RegistrationStatus validation |
| `TC-SCHEDULE-006` | `TC-FT-01`, `TC-FT-02` | UC05 | `WorkScheduleService.Manual` |
| `TC-SCHEDULE-001` | `TC05-02` | BR05 | `SchedulingValidationService`, `WorkScheduleService.Manual` |
| `TC-SCHEDULE-002` | `TC05-03` | BR06 | `LeaveRequestRepository.GetApprovedLeavesByPeriodAsync`, scheduling validation |
| `TC-SCHEDULE-003` | `TC05-04`, `TC-PT-01` | BR09 | `SchedulingValidationService` |
| `TC-SCHEDULE-004` | `TC05-05`, `TC-FT-05` | BR10 | `SchedulingValidationService` |
| `TC-SCHEDULE-014` | `TC-FT-02`, `TC-FT-07` | BR10 positive | `WorkScheduleService.Manual`, scheduling validation |
| `TC-SCHEDULE-018` | `TC-BR20-01`, `TC-LC04-01/02/03` | BR20 / BRD BR-03 | `SchedulingRestRuleEvaluator` |
| `TC-SCHEDULE-020` | `TC-BR20-03` | BR20 / BRD BR-03 | `SchedulingRestRuleEvaluator` |
| `TC-SCHEDULE-019` | `TC-BR21-01` | BR21 / BRD BR-08 | `SchedulingRestRuleEvaluator` |
| `TC-SCHEDULE-021` | `TC-BR21-02`, `TC-BR21-03`, `TC05-INT-01` | BR21 / BRD BR-08 | `SchedulingRestRuleEvaluator`, `SchedulingValidationService` |
| `TC-ORTOOLS-001` | `TC06-01` | UC06 / BR15 | `OrToolsSchedulingService`, `WorkScheduleService.OrTools` |
| `TC-ORTOOLS-002` | `TC06-02` | UC06 / BR15 | `OrToolsSchedulingService` |
| `TC-ORTOOLS-003` | `TC06-03`, `TC06-INT-01` | UC06 / BR15 | `WorkScheduleService.OrTools` |
| `TC-PUBLISH-003` | — manual HTTP + service flow | UC07 | `WorkScheduleController.Publish`, `WorkScheduleService.Publish` |
| `TC-PUBLISH-004` | `TC-BR20-03` + publish validation | UC07 / BR20 | `WorkScheduleService.Publish`, `SchedulingValidationService` |
| `TC-PUBLISH-005` | `TC07-INT-01` | UC07 / BR21 | `WorkScheduleService.Publish`, `SchedulingRestRuleEvaluator` |
| `TC-LEAVE-001` | `TC09-01` | UC09 | `LeaveRequestController`, `LeaveRequestService.SubmitAsync` |
| `TC-LEAVE-002` | `TC09-02` | UC09 | `LeaveRequestService.SubmitAsync` |
| `TC-LEAVE-005` | `TC09-06`, `TC09-07` | UC09 / BR16 | `LeaveRequestService.ReviewAsync` |
| `TC-TRANSFER-001` | `TC10-01`, `TC10-04`, `TC10-08` | UC10 / BR17 | `ShiftTransferService` |
| `TC-TRANSFER-003` | `TC10-07` | UC10 / BR17 | `ShiftTransferService.Validation` |
| `TC-TRANSFER-004` | `TC10-R11` | UC10 / BR21 | `ShiftTransferService.Validation`, rest validation |
| `TC-REPORT-001` | `TC11-01` | UC11 / BR18 | Report service/query |
| `TC-REPORT-002` | `TC11-02` | UC11 | CSV export |
| `TC-REPORT-004` | `TC11-03`, `TC-REPORT-CONSISTENCY-01/02/03` | UC11 | Report calculation using `ShiftType.DurationHours` |

## 5. Truy vết BR chính → testcase

| BR nội bộ | Nội dung kiểm chính | Testcase tài liệu | Automated test tiêu biểu |
|---|---|---|---|
| BR01 | Authentication / authorization | `TC-AUTH-002`, `TC-AUTH-003` | `TC01-02`, `TC03-A03` |
| BR02 | Chỉ Employee Active được xếp lịch | `TC-EMP-004` | `TC07-INT-02`, `TC05-BR02` |
| BR03 | 7 ngày, 3 ca cố định | `TC-PERIOD-001`, `TC-PERIOD-004` | `TC03-01..06`, `TC03-B01..B06` |
| BR04 | Availability theo trạng thái/deadline | `TC-AVAIL-001..003` | `TC04-01..03` |
| BR05 | Phải có Availability | `TC-SCHEDULE-001` | `TC05-02`, `TC10-R01` |
| BR06 | Approved Leave chặn phân công | `TC-SCHEDULE-002` | `TC05-03`, `TC10-R02` |
| BR07 | Đúng Position | Nhóm scheduling, truy vết tự động | `TC10-R03` |
| BR08 | Không overlap | Nhóm scheduling, truy vết tự động | `TC10-R04` |
| BR09 | PT tối đa 1 ca/ngày | `TC-SCHEDULE-003` | `TC05-04`, `TC10-R05` |
| BR10 | FT làm cặp liên tiếp | `TC-SCHEDULE-004`, `TC-SCHEDULE-014` | `TC05-05`, `TC-FT-02/05/07/08` |
| BR11 | FT tối đa 6 ngày | Nhóm scheduling, truy vết tự động | `TC10-R09`, `TC05-BR11` |
| BR12 | Đủ staffing | `TC-PUBLISH-003` + validation | `TC05-01`, `TC10-R10`, `TC03-C01..03` |
| BR13 | Có Level2+ | `TC-PUBLISH-003` + validation | `TC05-01`, `TC10-R07`, `TC03-D01..02` |
| BR14 | Draft được lưu; Published mới chính thức | `TC-ORTOOLS-003`, `TC-PUBLISH-003` | `TC06-03`, `TC06-INT-01` |
| BR15 | OR-Tools chỉ sinh Proposal | `TC-ORTOOLS-001..003` | `TC06-01..03` |
| BR16 | Re-read latest state trước thao tác nhạy cảm | `TC-LEAVE-005`, `TC-PUBLISH-005` | `TC09-07`, `TC10-R08`, `TC07-INT-02` |
| BR17 | Luồng đổi ca | `TC-TRANSFER-001`, `TC-TRANSFER-003` | `TC10-01..10` |
| BR18 | Workload warning/report | `TC-REPORT-001` | `TC11-01`, `TC-SCHEDULE-012` |
| BR19 | Fairness/optimization | OR-Tools regression | `TC-SCHEDULE-013` |
| BR20 / BRD BR-03 | Nghỉ tối thiểu 12h giữa hai ngày | `TC-SCHEDULE-018`, `TC-SCHEDULE-020`, `TC-PUBLISH-004` | `TC-BR20-01..03`, `TC-LC04-01..03` |
| BR21 / BRD BR-08 | ≥24h nghỉ liên tục trong 7 ngày | `TC-SCHEDULE-019`, `TC-SCHEDULE-021`, `TC-PUBLISH-005`, `TC-TRANSFER-004` | `TC-BR21-01..04`, `TC06-BR21-01`, `TC05-INT-01`, `TC07-INT-01`, `TC10-R11` |
## 6. Kết luận tiến độ
- Bộ testcase chính đã ưu tiên đúng phần lõi của đề tài: scheduling, OR-Tools, BR20, BR21, Publish,
  Leave, Transfer và Report.
- Mã testcase tài liệu không bị đổi; mã automated test trong code được ánh xạ riêng để truy vết.
- BR20 được ghi kèm quan hệ với **BRD BR-03**; BR21 được ghi kèm quan hệ với **BRD BR-08** để tránh
  nhầm hai hệ mã BR.
- Kết quả manual HTTP/UI cũ vẫn giữ commit gốc `779e73d`; không giả mạo thành kết quả của HEAD mới.

