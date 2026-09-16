# BUSINESS RULE TEST COVERAGE — BẢN ĐỐI CHIẾU SOURCE CUỐI

## 0. Phạm vi và mốc đối chiếu

Tài liệu này được rà soát theo snapshot source `ac18145` trên branch `overall`, đồng thời đối chiếu với:

- `testcase_final.md`;
- `matrantruyvet_final.md`;
- toàn bộ automated tests trong `src/backend/ShiftScheduling.Tests`;
- production code thực thi các quy tắc trong `src/backend/ShiftScheduling.Web`.

Mốc kiểm chứng dùng trong tài liệu:

- Testcase tài liệu: **258 mã duy nhất**.
- Kiểm kê tĩnh trong snapshot source: **589 runtime test cases**.
- Kết quả local gần nhất đã được cung cấp cho đúng bộ source/test hiện hành: **589/589 PASS, 0 Failed, 0 Skipped**.
- Controller Database Integration: **5/5 PASS**.
- Non-integration: **584/584 PASS**.
- Build Release `/warnaserror`: **0 warnings, 0 errors**.

> **Quy ước ánh xạ:** mã BR trong bảng này là mã BR hiện hành trong báo cáo. Một số automated test cũ còn giữ mã lịch sử khác trong `DisplayName` hoặc tên method. Việc ánh xạ được thực hiện theo **hành vi production code mà test thực sự kiểm tra**, không chỉ dựa vào số BR xuất hiện trong tên test.

> **Hai mã BR đặc biệt:** BR20 trong báo cáo tương ứng BRD `BR-03` về nghỉ tối thiểu 12 giờ; BR21 tương ứng BRD `BR-08` về nghỉ liên tục tối thiểu 24 giờ.

> Nhóm automated test lịch sử `TC-NC01-BR05-*` kiểm trần giờ tuần. Theo quy tắc báo cáo hiện hành, các test PT `≤ 28h` được ánh xạ về **BR09**, còn FT `≤ 48h` được ánh xạ về **BR10**.

---

## 1. Business Rule Test Coverage

| BR báo cáo | Quy tắc nghiệp vụ hiện hành | Testcase tài liệu tiêu biểu | Automated test / bằng chứng source tiêu biểu | Production code thực thi chính | Kỹ thuật / loại kiểm thử | Kết quả |
|---|---|---|---|---|---|---|
| **BR01** | Người dùng chỉ được thao tác đúng role; tài khoản Employee phải liên kết hồ sơ phù hợp; tài khoản bị khóa không đăng nhập. | `TC-AUTH-002`, `TC-AUTH-003`, `TC-ROLE-001`, `TC-ROLE-011`, `TC-ROLE-013` | `TC01-02`, `TC01-03`, `TC01-04`, `TC01-05`, `TC01-06`, authorization reflection tests | `AuthenticationService.cs`; `AccountService*.cs`; `[Authorize]` trên Controller/Action | Phân hoạch tương đương; bảng quyết định role × endpoint; authorization reflection | **Pass** |
| **BR02** | Chỉ Employee đang hoạt động được tham gia lịch mới; không deactivate khi còn assignment Published hiện tại/tương lai; Employee status độc lập với UserAccount lock. | `TC-SCHEDULE-007`, `TC-EMP-001`, `TC-EMP-002`, `TC-EMP-003` | `TC02-05`, `TC02-06`, `TC02-07`, `TC02-08`, `TC02-09`, `TC02-10`, `TC07-INT-02` | `EmployeeService.cs`; `WorkScheduleService.Manual*.cs`; `SchedulingValidationService.cs` | Bảng quyết định trạng thái; regression lifecycle | **Pass** |
| **BR03** | Mỗi kỳ dùng đúng ba mã MORNING/AFTERNOON/EVENING thuộc cùng configuration version; thay đổi cấu hình tạo version mới để kỳ cũ không đổi. | `TC-PERIOD-001`, `TC-PERIOD-004`, `TC-SHIFT-CONFIG-001`, `TC-SHIFT-CONFIG-002` | `TC03-01`, `TC03-02`, `TC03-03`, `TC03-04`, `TC03-05`, `TC03-06`; nhóm `TC-SHIFT-CONFIG-*` | `ShiftTypeService.cs`; `SchedulePeriodService.Creation.cs`; period-specific ShiftType version loading | Phân hoạch tương đương; kiểm version; regression dữ liệu lịch sử | **Pass** |
| **BR04** | Employee chỉ đăng ký/cập nhật Availability khi kỳ Open, chưa hết deadline và chưa bắt đầu. | `TC-AVAIL-001`, `TC-AVAIL-002`, `TC-AVAIL-003` | `TC04-01`, `TC04-02`, `TC04-03`; NC-02 registration regressions | `AvailabilityService.Registration.cs`; `SchedulePeriodStatusHelper.cs` | Phân hoạch tương đương; biên deadline; chuyển trạng thái | **Pass** |
| **BR05** | Employee chỉ được phân vào ngày-ca đã đăng ký rảnh; hệ thống không dùng ca không rảnh để bù staffing. | `TC-SCHEDULE-001`, `TC-NC02-002`, `TC-LEAVE-006`, `TC-LEAVE-007` | `TC05-02`, `TC10-R01`, `TC-NC02-16`; leave replacement candidate tests | `SchedulingValidationService.cs`; `WorkScheduleService.Manual.Eligibility.cs`; `OrToolsSchedulingService.Constraints.cs`; leave/transfer validation | Phân hoạch tương đương – vi phạm eligibility; regression | **Pass** |
| **BR06** | Approved Leave chặn phân công ở ngày/ca tương ứng và được dùng khi tính giờ công, coverage, replacement. | `TC-SCHEDULE-002`, `TC-NC07-001`, `TC-REPORT-012`, `TC-REPORT-013`, `TC-REPORT-014`, `TC-REPORT-015` | `TC05-03`, `TC10-R02`; nhóm `NC07-*`; `TC-REPORT-012..015` | `SchedulingValidationService.cs`; `ApprovedLeaveLookup`; `WorkReportService*.cs`; `WorkScheduleService.LeaveReplacement*.cs` | Phân hoạch exact-shift/full-day/status; regression liên chức năng | **Pass** |
| **BR07** | Phân công, transfer và replacement phải đúng Position; cơ cấu staffing theo vị trí vẫn phải hợp lệ. | `TC-SCHEDULE-008`, `TC-LEAVE-006`, `TC-LEAVE-007`, `TC-LEAVE-008`, `TC-TRANSFER-001` | `TC10-R03`; manual eligibility/replacement candidate regressions | `WorkScheduleService.Manual.Eligibility.cs`; `SchedulingValidationService.Staffing.cs`; `ShiftTransferService.Validation.cs` | Phân hoạch tương đương – sai Position; revalidation | **Pass** |
| **BR08** | Không cho assignment trùng hoặc các ca chồng lấn thời gian của cùng Employee. | `TC-SCHEDULE-009`, `TC-SCHEDULE-017` | `TC10-R04`, `TC-BR01-FT-03`; overlap/duplicate regressions | `DailyShiftRuleEvaluator.cs`; `SchedulingValidationService.cs`; manual/transfer validation | Phân hoạch duplicate/overlap; regression | **Pass** |
| **BR09** | Part-time tối đa 1 ca/ngày và tối đa 28 giờ trong kỳ 7 ngày. | `TC-SCHEDULE-003`, `TC-NC01-BR05-003`, `TC-NC01-BR05-004`, `TC-NC01-BR05-006`, `TC-NC01-BR05-012`, `TC-NC01-BR05-014`, `TC-NC01-BR05-017` | `TC05-04`, `TC-PT-01`, `TC10-R05`; nhóm PT trong `TC-NC01-BR05-*` | `DailyShiftRuleEvaluator.cs`; `WeeklyWorkHoursRuleEvaluator.cs`; `SchedulingValidationService.cs`; `OrToolsSchedulingService.Constraints.cs` | Phân tích giá trị biên: 1/2 ca; 28h/>28h; manual/publish/solver | **Pass** |
| **BR10** | Full-time tuân theo `MaxShiftsPerEmployeePerDay` của configuration version (1 hoặc 2); 1 ca/ngày vẫn hợp lệ; nếu Max=2 và không cho split thì cấm Morning+Evening; tổng giờ tuần không vượt 48 giờ. | `TC-SCHEDULE-004`, `TC-SCHEDULE-014`, `TC-SCHEDULE-015`, `TC-SCHEDULE-016`, `TC-NC01-BR05-001`, `TC-NC01-BR05-002`, `TC-NC01-BR05-008`, `TC-NC01-BR05-009`, `TC-NC01-BR05-013`, `TC-NC01-BR05-014` | `TC05-05`; `TC-FT-01..09`; `TC-BR01-FT-01..03`; `TC-BR01-OR-01..02`; FT weekly-hours regressions | `DailyShiftRuleEvaluator.cs`; `WeeklyWorkHoursRuleEvaluator.cs`; `SchedulingValidationService.cs`; `OrToolsSchedulingService.Constraints.cs` | Bảng quyết định policy 1/2 ca; split/no-split; biên 48h/>48h | **Pass** |
| **BR11** | Full-time không được làm quá 6 ngày trong kỳ 7 ngày. | `TC-SCHEDULE-010` | `TC10-R09`; BR11 validation/solver regressions | `SchedulingValidationService.cs`; `OrToolsSchedulingService.Constraints.cs`; `SchedulingRuleConstants.FullTimeMaxWorkDaysPerWeek` | Phân tích biên 6/7 ngày | **Pass** |
| **BR12** | Mỗi ngày-ca phải đúng định mức Sales/Cashier trước Publish; thiếu hoặc dư đều là hard violation. | `TC-SCHEDULE-005`, `TC-PUBLISH-001`, `TC-PUBLISH-003` | `TC05-01`, `TC05-06`, `TC05-07`, `TC03-C01`, `TC03-C02`, `TC03-C03`, `TC10-R10` | `SchedulingValidationService.Staffing.cs`; `SchedulePeriodService.Staffing.cs`; `OrToolsSchedulingService.Constraints.cs` | Phân hoạch thiếu/đủ/dư; validation trước Publish | **Pass** |
| **BR13** | Ca có nhu cầu phải có ít nhất một Employee Level 2+ theo cấu hình và điều kiện này phải được giữ sau thay đổi lịch. | `TC-SCHEDULE-005`, `TC-SCHEDULE-011`, `TC-PUBLISH-001`, `TC-PUBLISH-003` | `TC05-01`, `TC05-08`, `TC03-D01`, `TC03-D02`, `TC10-R07` | `SchedulingValidationService.Staffing.cs`; `SchedulePeriodService.Staffing.cs`; transfer/replacement revalidation | Phân tích biên Level; publish/revalidation | **Pass** |
| **BR14** | Draft được lưu/chỉnh sửa nhưng không phải lịch chính thức; Employee/report không sử dụng Draft hoặc Proposal. | `TC-ORTOOLS-003`, `TC-PUBLISH-001`, `TC-PUBLISH-003`, `TC-MYSCHEDULE-002`, `TC-REPORT-002` | `TC06-03`, `TC06-INT-01`, `MS-06`, Published-only report regressions | `WorkScheduleService.OrTools.cs`; `WorkScheduleService.Publish.cs`; `MyScheduleController.cs`; `WorkReportService.cs` | Chuyển trạng thái Draft→Published; negative visibility regression | **Pass** |
| **BR15** | OR-Tools chỉ tạo Proposal; không tự lưu thành lịch chính thức và không tự Publish. | `TC-ORTOOLS-001`, `TC-ORTOOLS-002`, `TC-ORTOOLS-003` | `TC06-01`, `TC06-02`, `TC06-03`, `TC06-INT-01` | `OrToolsSchedulingService*.cs`; `WorkScheduleService.OrTools.cs` | Khả thi/không khả thi; state regression | **Pass** |
| **BR16** | Trước Publish hoặc cập nhật lịch chính thức phải đọc dữ liệu mới nhất và revalidate; state guard chặn xử lý lặp; các luồng có RowVersion dùng token để phát hiện stale update. | `TC-PUBLISH-002`, `TC-LEAVE-004`, `TC-LEAVE-007`, `TC-LEAVE-011`, `TC-TRANSFER-005`, `TC-DB-001` | `TC07-INT-02`, `TC09-07`, `TC10-R08`; RowVersion/concurrency regressions | `WorkScheduleService.Publish.cs`; `LeaveRequestService*.cs`; `WorkScheduleService.LeaveReplacement*.cs`; `ShiftTransferService*.cs` | State transition; stale data; optimistic concurrency | **Pass** |
| **BR17** | Chuyển ca là chuyển một chiều: Sender tạo → Receiver phản hồi → Manager quyết định; assignment chỉ đổi sang Receiver khi Receiver còn hợp lệ. | `TC-TRANSFER-001`, `TC-TRANSFER-002`, `TC-TRANSFER-003`, `TC-TRANSFER-004`, `TC-TRANSFER-005`, `TC-NC07-003`, `TC-REPORT-016` | `TC10-01..10`; `TC10-R01..R11`; transfer weekly-hours/report regressions | `ShiftTransferService*.cs`; `ShiftTransferController.cs`; `ShiftAssignment.Source=ShiftTransfer` | State transition; bảng quyết định; revalidation | **Pass** |
| **BR18** | Workload mục tiêu chỉ tạo warning: PT so với khoảng 5 ca/tuần, FT so với 6 ngày/tuần; warning không làm lịch thành invalid. | `TC-SCHEDULE-012`, `TC-SCHEDULE-022`, `TC-SCHEDULE-023` | `TC11-01`; workload warning regressions | `SchedulingValidationService.GetWorkloadWarning`; `SchedulingRuleConstants.PartTimeTargetShiftsPerWeek`; `AddFairnessWarnings` | Warning-vs-hard-error regression; boundary workload | **Pass** |
| **BR19** | Với nhiều lịch hợp lệ, OR-Tools dùng **một weighted-sum objective** được thiết kế để giữ thứ tự ưu tiên: cân bằng tổng phút trong từng nhóm FT/PT → Preferred → ưu tiên ghép ca FT liên tiếp → hạn chế Evening liên tiếp. Đây không phải true lexicographic multi-solve. Fairness warning/report là chỉ số quan sát riêng. | `TC-SCHEDULE-013`, `TC-ORTOOLS-005`, `TC-ORTOOLS-006`, `TC-FAIR-001`, `TC-FAIR-002`, `TC-FAIR-003`, `TC-FAIR-004`, `TC-FAIR-005` | `TC-BR10-01..06`; `TC-ORT-FT-01`, `TC-ORT-FT-02`; `FAIR-01..05` | `OrToolsSchedulingService.Objective.cs`; `SchedulingValidationService.AddFairnessWarnings`; `WorkReportService.Fairness.cs` | Optimization regression; deterministic comparative scenarios; report aggregation | **Pass** |
| **BR20 / BRD BR-03** | Giữa ca cuối ngày D và ca đầu ngày D+1 phải có ít nhất 12 giờ nghỉ; đúng 12 giờ là hợp lệ. | `TC-SCHEDULE-018`, `TC-SCHEDULE-020`, `TC-PUBLISH-004`, `TC-LC-003`, `TC-LC-004`, `TC-LC-005` | `TC-BR20-01`, `TC-BR20-02`, `TC-BR20-03`; `TC-LC04-01..03`; OR-Tools BR20 regressions | `SchedulingRestRuleEvaluator.cs`; scheduling/manual/publish/OR-Tools validation | Phân tích giá trị biên: 11h/12h/>12h; biên tháng/năm/ngày nhuận | **Pass** |
| **BR21 / BRD BR-08** | Trong kỳ 7 ngày mỗi Employee phải có ít nhất một khoảng nghỉ liên tục 24 giờ; đúng 24 giờ là hợp lệ. | `TC-SCHEDULE-019`, `TC-SCHEDULE-021`, `TC-PUBLISH-005`, `TC-TRANSFER-004`, `TC-ORTOOLS-004` | `TC-BR21-01..04`, `TC06-BR21-01`, `TC05-INT-01`, `TC07-INT-01`, `TC10-R11` | `SchedulingRestRuleEvaluator.cs`; `SchedulingValidationService.cs`; `OrToolsSchedulingService.Constraints.cs`; transfer validation | Phân tích giá trị biên 24h/<24h; integration publish/transfer/solver | **Pass** |
| **BR22** | Shift Transfer phải đi theo thứ tự Sender tạo → Receiver phản hồi → Manager quyết định; request đã phản hồi/xử lý không được xử lý lặp và retry không tạo notification/email kết quả trùng. | `TC-TRANSFER-001`, `TC-TRANSFER-003`, `TC-FORM-TRANSFER-005`, `TC-FORM-RESPOND-003`, `TC-FORM-TRANSFER-REVIEW-005`, `TC-NC10-003` | `TC10-02`, `TC10-04`, `TC10-05`, `TC10-06`, `TC10-07`, `TC10-08`, `TC10-09`; `N10-02`, `N10-05`, `N10-06`, `N10-F03` | `ShiftTransferService.cs`; `ShiftTransferService.Helpers.cs`; `ShiftTransferService.Notifications.cs` | State machine; idempotency/retry regression; notification duplicate guard | **Pass** |

---

## 2. Kết quả bao phủ BR

| Nội dung | Kết quả |
|---|---:|
| Tổng Business Rule trong báo cáo hiện hành | **22** |
| BR có testcase tài liệu đại diện | **22 / 22** |
| BR có automated test / regression liên quan | **22 / 22** |
| BR có production code được truy vết | **22 / 22** |
| BR chưa có coverage | **0** |
| Testcase tài liệu hiện hành | **258 mã duy nhất** |
| Automated runtime cases của solution | **589** |
| Kết quả local gần nhất | **589 / 589 PASS** |

> Số **258** là số testcase tài liệu. Số **589** là số automated runtime test cases trong solution. Hai con số không được cộng với nhau và không được dùng thay thế cho nhau.

---

## 3. Các điểm phải giữ đúng khi đưa vào báo cáo

### 3.1. BR10 không còn là “FT bắt buộc đúng 2 ca liên tiếp”

Source hiện hành cho phép:

- `MaxShiftsPerEmployeePerDay = 1` hoặc `2` theo configuration version;
- FT có **1 ca/ngày vẫn hợp lệ**;
- nếu Max=2 và `AllowSplitShift=false`, tổ hợp Morning+Evening bị cấm;
- tổng thời lượng FT trong kỳ không vượt **48 giờ**.

Việc ưu tiên xếp hai ca liền nhau cho FT trong OR-Tools là **soft preference** thuộc nhóm objective BR19, không phải hard constraint BR10.

### 3.2. BR19 dùng weighted-sum objective

Source hiện tại dùng một hàm mục tiêu tổng có trọng số để ưu tiên:

1. giảm chênh lệch tổng phút làm việc trong từng nhóm FT/PT;
2. Preferred hơn Normal;
3. ưu tiên cặp ca liên tiếp cho FT;
4. hạn chế Evening ở hai ngày liên tiếp.

Đây là cách thiết kế trọng số để mô phỏng thứ tự ưu tiên trong **một objective**, không phải quy trình lexicographic nhiều lần giải solver.

### 3.3. BR09 và BR10 nhận coverage từ nhóm test có mã lịch sử `BR05`

Các automated test `TC-NC01-BR05-*` được tạo khi tài liệu còn dùng cách đánh số BR khác. Khi lập báo cáo cuối:

- testcase kiểm PT tối đa 28 giờ → ánh xạ **BR09**;
- testcase kiểm FT tối đa 48 giờ → ánh xạ **BR10**.

Không đổi tên test trong source chỉ để làm đẹp tài liệu.

### 3.4. BR22 phải có trong coverage cuối

BR22 là quy tắc state machine/idempotency của Shift Transfer:

**Sender tạo → Receiver phản hồi → Manager quyết định**.

Các thao tác trên request đã xử lý bị chặn, đồng thời notification/email kết quả không được tạo trùng khi request bị gọi lại.

---

## 4. Regression quan trọng hỗ trợ BR nhưng không tạo BR mới

| Nhóm | Bằng chứng chính | Ý nghĩa |
|---|---|---|
| Lifecycle `Draft / Published / Active / Closed` | `TC-LIFECYCLE-001..009`; automated `TC-LC-101..121` | Active là trạng thái suy ra; chỉ Close sau khi kỳ kết thúc; Closed read-only nhưng vẫn đọc lịch sử. |
| Workday Adjustment / NC-07 | `TC-NC07-001..003`; `TC-REPORT-005..011`, `TC-REPORT-018`; automated `NC07-*` | Leave = 0h, override/reset, ca qua đêm, audit, security, consistency Manager/Employee/CSV, regression decimal culture. |
| Coverage / NC-08 | `TC-NC08-001..010`; `TC-REPORT-012..016` | Coverage theo đúng ngày-ca-vị trí; không bù chéo; Approved Leave được loại khỏi slot được phủ. |
| Notification / Email / NC-10 | `TC-NC10-001..005`; automated `N10-*` | Publish/leave/transfer/open-registration; email lỗi không rollback business; chống duplicate; security. |
| Demo 4 kỳ / reset-demo | `TC-SEED-001..005`; automated `TC-SEEDREL-*`, `TC-SEEDRESET-*` | Demo Closed/Active/Draft/Open-registration; restart idempotent; reset phá hủy chỉ khi explicit opt-in và DB hậu tố `_Demo`. |
| Portable database integration | `TC-DB-002`; IT-01..IT-05 | Dedicated `ShiftSchedulingDb_IntegrationTest`; hỗ trợ LocalDB hoặc SQL Server TCP; không fallback sang DB demo/production. |

---

## 5. Kiểm tra tính nhất quán

- BR trong báo cáo hiện hành: **22/22 có coverage**.
- Không còn dùng tổng cũ **21/21**.
- BR22 đã được bổ sung.
- BR10 đã sửa đúng semantics source hiện tại.
- BR19 đã mô tả đúng weighted-sum objective.
- BR20 và BR21 giữ mapping BRD tương ứng `BR-03` và `BR-08`.
- Các mã testcase tài liệu trong bảng đều thuộc bộ `testcase_final.md`.
- Các automated test được dẫn chiếu từ snapshot source `ac18145`.
- Không dùng kết quả manual/UI lịch sử để suy diễn rằng toàn bộ 258 testcase đã được chạy lại thủ công ở ngày hiện tại.
- Kết quả **589/589 PASS** chỉ phản ánh automated suite đã chạy; không thay thế cho trạng thái manual test lịch sử.

---

## 6. Quan hệ với các tài liệu kiểm thử khác

- `testcase_final.md`: mô tả chi tiết testcase, dữ liệu đầu vào, bước chạy, expected/actual result và trạng thái.
- `matrantruyvet_final.md`: truy vết FR/BR → testcase → automated evidence → production code.
- File này: tập trung trả lời **mỗi Business Rule được kiểm bằng testcase nào, bằng chứng automated nào và production code nào thực thi quy tắc đó**.

Ba tài liệu phải được cập nhật đồng thời nếu một Business Rule, testcase hoặc source behavior thay đổi.
