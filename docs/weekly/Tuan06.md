# NHẬT KÝ ĐỒ ÁN NGÀNH – TUẦN 6

**Tên đề tài:** Hệ thống xếp lịch ca làm việc và quản lý nhân sự
**Tên tiếng Anh:** Employee Shift Scheduling & Workforce Management System
**Sinh viên:** Nguyễn Thanh Bình

## 1. Công việc đã thực hiện

Trong tuần 6, em tập trung rà soát và bổ sung các yêu cầu liên quan đến cơ sở dữ liệu, kiến trúc hệ thống, xử lý ngày giờ, các quy tắc xếp lịch và kiểm thử.

Các nội dung chính đã thực hiện:

* Rà soát cấu trúc cơ sở dữ liệu, migration và file `database/schema.sql`.
* Bổ sung cơ chế **optimistic concurrency** bằng `RowVersion` cho một số dữ liệu có khả năng được nhiều người cập nhật đồng thời, gồm:

  * Đơn xin nghỉ.
  * Yêu cầu đổi ca.
  * Lịch làm việc.
* Bổ sung xử lý trường hợp xảy ra xung đột cập nhật và mã lỗi `CONCURRENCY_CONFLICT`.
* Rà soát các truy vấn dữ liệu, hạn chế truy vấn dư thừa và hiện tượng N+1.
* Rà soát kiến trúc ASP.NET Core MVC theo hướng:

  * Controller không truy cập trực tiếp `DbContext`.
  * Business logic được xử lý tại Service.
  * Repository phụ trách truy cập dữ liệu.
  * Không trả trực tiếp Entity cho giao diện.
* Chuẩn hóa xử lý ngày giờ:

  * Lưu dữ liệu thời gian theo UTC.
  * Chuyển sang múi giờ Việt Nam khi hiển thị.
  * Giao diện sử dụng định dạng ngày `dd/MM/yyyy` và giờ 24 giờ.
* Bổ sung kiểm thử các trường hợp ngày tháng đặc biệt như:

  * Qua cuối tháng.
  * Qua cuối năm.
  * Ngày nhuận.
* Cấu hình collation `Vietnamese_100_CI_AI` cho các trường dữ liệu dạng văn bản cần hỗ trợ tìm kiếm và so sánh tiếng Việt, chẳng hạn họ tên nhân viên và tên ca.
* Rà soát và kiểm thử các quy tắc xếp lịch, trong đó tập trung vào:

  * **BR20:** Nhân viên phải có ít nhất 12 giờ nghỉ liên tục giữa hai ngày làm việc.
  * **BR21:** Trong mỗi khoảng 7 ngày, nhân viên phải có ít nhất một khoảng nghỉ liên tục 24 giờ.
* Rà soát chức năng sinh lịch bằng OR-Tools, bảo đảm kết quả sinh tự động chỉ là **phương án đề xuất**, không tự động công bố lịch.
* Rà soát chức năng báo cáo tổng giờ làm việc và sử dụng `ShiftType.DurationHours` để tính theo thời lượng thực tế của từng ca.
* Mở rộng bộ test case tuần 6 theo các yêu cầu TC-06, TC-11 và TC-12.
* Bổ sung các trường hợp kiểm thử cho:

  * Luồng thành công và luồng ngoại lệ của Use Case.
  * Business Rule.
  * Dữ liệu nhập không hợp lệ.
  * Phân quyền giữa Admin, Manager và Employee.
  * Truy cập trái phép vào dữ liệu của người dùng khác.
  * Optimistic concurrency.
  * BR20 và BR21.
  * OR-Tools.
  * Timezone và collation.

## 2. Tình hình kiểm thử


Em đã thực hiện kiểm thử trên ứng dụng ASP.NET Core MVC đang chạy thực tế và đối chiếu kết quả với dữ liệu trong SQL Server khi cần thiết.

Các nhóm chức năng đã được kiểm thử gồm:

* Đăng nhập và phân quyền.
* Quản lý nhân viên và kỳ lịch.
* Đăng ký lịch rảnh.
* Phân công nhân viên.
* Kiểm tra BR20 và BR21.
* Sinh lịch bằng OR-Tools.
* Công bố lịch.
* Xem lịch cá nhân.
* Đơn xin nghỉ.
* Đổi ca.
* Báo cáo số ca, số ngày và tổng giờ.
* Xử lý múi giờ Việt Nam.
* Collation tiếng Việt.
* Kiểm tra cập nhật đồng thời bằng `RowVersion`.

**Lưu ý:** Các test case đang ở trạng thái `Fail` chưa được kết luận toàn bộ là lỗi hệ thống. Em đang tiếp tục phân loại để xác định trường hợp nào là lỗi chương trình, trường hợp nào do Expected Result của test case chưa phù hợp với thiết kế thực tế, và trường hợp nào chưa đủ evidence để kết luận.

## 3. Kết quả đạt được

Sau các thay đổi của tuần 6:

* Hệ thống vẫn build và chạy được.
* Kiến trúc Controller – Service – Repository được duy trì.
* Cách lưu và hiển thị ngày giờ đã được rà soát theo UTC và múi giờ Việt Nam.
* Bổ sung kiểm thử cho các trường hợp biên về ngày tháng.
* Bổ sung kiểm thử cho BR20 và BR21.
* Chức năng OR-Tools được kiểm tra theo hướng sinh **phương án đề xuất**, không tự động công bố.
* Báo cáo tổng giờ sử dụng thời lượng thực tế của từng loại ca.
* Collation tiếng Việt được áp dụng cho các trường dữ liệu phù hợp.
* Bộ test case được mở rộng lên 150 test case và đã có kết quả chạy thực tế.
* Bổ sung kiểm tra phân quyền thông qua URL và trường hợp người dùng cố gắng truy cập dữ liệu không thuộc quyền của mình.
* Bổ sung kiểm thử optimistic concurrency đối với các chức năng có khả năng xảy ra cập nhật đồng thời.

## 4. Nội dung còn tồn tại

Một số nội dung vẫn đang được tiếp tục xử lý:

* Chưa chốt trạng thái cuối cùng của toàn bộ 150 test case.
* 43 test case đang `Fail`, trong đó có một số trường hợp cần xác định lại Expected Result trước khi kết luận là defect.
* 5 test case concurrency đang `Blocked` do test case hiện tại kỳ vọng `HTTP 409 Conflict`, trong khi một số luồng MVC thực tế đang xử lý lỗi bằng `HTTP 302 Redirect`.
* Cần thống nhất cách xử lý lỗi concurrency giữa Global Exception Handler và các Controller xử lý form MVC.
* Cần tiếp tục rà soát các test case liên quan đến HTTP response của `WorkSchedule`, `Publish`, `Availability` và một số form khác.
* Chưa hoàn tất defect report cho những trường hợp được xác định là lỗi thực tế của hệ thống.
* Cần kiểm tra thêm yêu cầu CI-02, đặc biệt là việc GitHub Actions đã đủ hay có cần cấu hình thêm branch protection và required status check.

## 5. Khó khăn trong quá trình kiểm thử

Khó khăn chính trong tuần này là phân biệt giữa **lỗi của hệ thống** và **sự không phù hợp giữa test case với cách thiết kế ASP.NET Core MVC**.

Hệ thống sử dụng ASP.NET Core MVC + Razor nên một số form sau khi xử lý sẽ thực hiện redirect, ví dụ:

```text
POST
→ xử lý nghiệp vụ
→ RedirectToAction
→ HTTP 302
```

Trong khi một số test case ban đầu được xây dựng theo giả định API trả về JSON và `HTTP 200`, `HTTP 400` hoặc `HTTP 409`.

Vì vậy, có trường hợp nghiệp vụ đã được xử lý đúng nhưng test case vẫn bị đánh `Fail` do HTTP status thực tế khác với Expected Result.

Ví dụ:

* Một số trường hợp validation trả về `HTTP 400`.
* Một số form sử dụng `TempData` và redirect bằng `HTTP 302`.
* Một số trường hợp concurrency được Controller bắt lỗi và redirect về trang trước thay vì trả trực tiếp `HTTP 409 Conflict`.

Do đó, khi đánh giá một test case, em đang kiểm tra đồng thời:

1. HTTP status.
2. Nội dung response hoặc thông báo hiển thị.
3. Trạng thái dữ liệu sau khi thực hiện thao tác.
4. Kết quả nghiệp vụ có đúng với Business Rule hay không.

## 6. Nội dung xin thầy hướng dẫn

Em xin thầy hướng dẫn thêm một số điểm để em thống nhất cách làm trước khi chốt bộ kiểm thử:

**1. Về HTTP status trong ASP.NET Core MVC**

Với project sử dụng ASP.NET Core MVC + Razor, đối với các form xử lý bằng `RedirectToAction` và `TempData`, nếu nghiệp vụ được xử lý đúng nhưng HTTP response là `302`, thầy cho em hỏi cách này có được chấp nhận không, hay thầy muốn các trường hợp lỗi phải trả về một HTTP status cụ thể như `400` hoặc `409`?

**2. Về Expected Result của test case**

Nếu Business Rule được thực hiện đúng nhưng HTTP status thực tế khác với Expected Result ban đầu của test case, thầy cho em hỏi em nên:

* Điều chỉnh Expected Result để phù hợp với thiết kế MVC đã chọn; hay
* Điều chỉnh Controller để đáp ứng HTTP status đã đặt trong test case?

Em muốn chốt nguyên tắc này trước khi đánh giá lại các test case `Fail`.

**3. Về TC-11**

Một số form không có những loại dữ liệu như số âm, ngày tháng hoặc trường UNIQUE.

Trong trường hợp này, em có thể thiết kế test case âm dựa trên **các điều kiện không hợp lệ thực tế của từng form** thay vì cố tạo một loại dữ liệu không phù hợp với chức năng hay không?

**4. Về CI-02**

Hiện tại project đã có GitHub Actions để build/test.

Thầy cho em hỏi CI-02 có yêu cầu bắt buộc phải cấu hình thêm **Branch Protection / Required Status Check** trên GitHub hay chỉ cần có quy trình CI thực hiện build và test tự động?

Em sẽ dựa trên hướng dẫn của thầy để chốt lại test case, phân loại defect và hoàn thiện báo cáo tuần 6.
