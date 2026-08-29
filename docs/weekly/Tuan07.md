# TUẦN 07 — HOÀN THIỆN NGHIỆP VỤ VÀ KIỂM THỬ

## 1. Mục tiêu

* Hoàn thiện các Business Rule còn thiếu.
* Kiểm thử các trường hợp biên và các luồng nghiệp vụ chính.
* Rà soát OR-Tools, Employee lifecycle và Login.
* Cập nhật testcase và ma trận truy vết.
* Đảm bảo automated test chạy ổn định.

## 2. Công việc đã thực hiện

### 2.1. Hoàn thiện BR20 và BR21

**BR20 — Nghỉ tối thiểu 12 giờ giữa hai ngày**

Nhân viên phải có ít nhất 12 giờ nghỉ giữa ca cuối ngày hôm trước và ca đầu ngày hôm sau.

Đã kiểm tra:

* Nhỏ hơn 12 giờ.
* Đúng 12 giờ.
* Lớn hơn 12 giờ.
* Qua cuối tháng.
* Qua cuối năm.
* Ngày nhuận.

**BR21 — Nghỉ tối thiểu 24 giờ trong 7 ngày**

Nhân viên phải có ít nhất 24 giờ nghỉ liên tục trong mỗi khoảng 7 ngày.

Đã kiểm tra:

* Đủ đúng 24 giờ.
* Không đủ 24 giờ.
* Áp dụng khi xếp lịch.
* Áp dụng với OR-Tools.
* Kiểm tra lại khi Publish lịch.

## 3. Rà soát OR-Tools

OR-Tools chỉ có nhiệm vụ **sinh lịch đề xuất**.

Luồng hiện tại:

`Sinh lịch → Proposal → Kiểm tra → Manager xem xét → Adopt → Draft → Publish`

OR-Tools **không tự động Publish** lịch.

Proposal cũng phải đáp ứng các quy tắc cứng trước khi được sử dụng.

## 4. Rà soát Employee và UserAccount

Đã kiểm tra lại trạng thái Employee và tài khoản.

* Không cho ngưng hoạt động Employee nếu còn lịch Published từ hiện tại trở đi.
* Lịch trong quá khứ không chặn việc ngưng hoạt động.
* Lịch Draft không được xem là lịch chính thức để chặn.
* `Employee.IsActive` và `UserAccount.IsLocked` là hai trạng thái độc lập.
* Reactivate Employee không tự động mở khóa tài khoản.
* Tài khoản bị khóa không được đăng nhập.

## 5. Bảo vệ Login

Đã bổ sung giới hạn request cho Login:

* Tối đa 10 request/phút/IP.
* Khi vượt giới hạn trả HTTP `429`.
* Có `Retry-After`.
* Không thay đổi logic xác thực tài khoản.

## 6. Kiểm thử

Đã chạy toàn bộ automated test.

```text
Passed: 140
Failed: 0
Skipped: 0
Total: 140
```

**Kết quả: 140/140 PASS**

Các nhóm được kiểm tra gồm:

* Authentication / Authorization.
* Employee.
* Availability.
* Manual Scheduling.
* OR-Tools.
* Publish.
* Leave Request.
* Shift Transfer.
* BR20.
* BR21.
* Concurrency.
* Report.

## 7. Cập nhật testcase và truy vết

Đã cập nhật `docs/kiem-thu/testcase.md`:

* Chuẩn hóa mã testcase.
* Bổ sung Expected Result cụ thể.
* Bổ sung Success Flow và Exception Flow.
* Bổ sung kỹ thuật kiểm thử.

Các kỹ thuật chính:

* Phân hoạch tương đương.
* Phân tích giá trị biên.
* Bảng quyết định.
* Kiểm thử chuyển trạng thái.
* Kiểm thử hồi quy.

Đã bổ sung:

`docs/kiem-thu/matran-truyvet.md`

Dùng để truy vết:

`FR / BR → Testcase → Automated Test`

## 8. Vấn đề cần GVHD giải đáp

### UC09 — Duyệt nghỉ khi lịch đã Published

Nếu Manager duyệt đơn nghỉ nhưng nhân viên đã có Assignment trong lịch Published, hệ thống hiện có thể phát hiện xung đột.

Tuy nhiên, lịch Published hiện không cho sửa trực tiếp như Draft.

Em chưa tự sửa source ở phần này vì nếu cho sửa Published sẽ làm thay đổi nghiệp vụ của UC09. Em có định hướng khi nhân viên xin nghỉ quản lý sẽ xử lý thêm người là nghiệp vụ ở bên ngoài không cập nhật vào lịch.

**Cần GVHD giải đáp:**

1. Chỉ cảnh báo để Manager xử lý theo quy trình khác.

hoặc

2. Cho phép có một luồng điều chỉnh lịch Published có kiểm soát.

Sau khi có xác nhận, em sẽ cập nhật source và testcase tương ứng.
