# Báo cáo tiến độ Tuần 08

## 1. Mục tiêu trong tuần

- Khắc phục các nhận xét của Tuần 07.
- Hoàn thiện những chức năng đang làm dở.
- Kiểm tra khả năng chạy từ bản clone mới.
- Bổ sung test và chuẩn bị Pull Request Tuần 08.

## 2. Công việc đã thực hiện

### 2.1. Khắc phục khả năng chạy bản clone sạch
- Bổ sung connection string mẫu trong `appsettings.Example.json`; README hướng dẫn User Secrets, LocalDB, dotnet-ef và năm bước chạy nhanh.
- Đã thử với database mới và đăng nhập được Admin, Manager và Employee; README đã ghi đủ ba tài khoản demo.

### 2.2. Hoàn thiện thông báo nội bộ
- Có danh sách thông báo, badge chưa đọc và đánh dấu đã đọc; chưa có Email hoặc SMS.
- Khi Quản lý mở đăng ký lịch rảnh, Employee nhận thông báo và có thể đi tới trang đăng ký.

### 2.3. Bổ sung AuditLog
- Ghi nhật ký khi Quản lý gỡ nhân viên khỏi ca thủ công; lịch đã công bố vẫn không cho phép chỉnh sửa.

### 2.4. Báo cáo và giới hạn giờ làm
- Bổ sung tỷ lệ phủ ca trong báo cáo; tổng giờ được tính theo thời lượng ca thực tế.
- PartTime tối đa 28 giờ/tuần (mục tiêu mềm 20 giờ/tuần), FullTime tối đa 48 giờ/tuần.

### 2.5. Cấu hình giờ làm việc
- Quản lý chỉnh giờ của cả ba ca trong cùng một lần lưu; không cho phép các ca chồng lấn.
- Cấu hình được lưu theo phiên bản để không làm thay đổi lịch cũ; số lượng ca trong ngày hiện vẫn cố định là ba.

### 2.6. Kiểm thử và tài liệu
- Bổ sung test cho các chức năng làm trong Tuần 08; loại ba test thông báo bị trùng hoặc không đúng phạm vi.
- Cập nhật test case phân quyền, tỷ lệ phủ ca và giới hạn giờ làm việc trong tuần.

## 3. Kết quả kiểm tra

- Build Release: 0 warning, 0 error.
- Kiểm thử tự động: 234/234 test đạt.
- Bản clone mới áp dụng thành công 6 bản migration và chạy được ứng dụng; đăng nhập được bằng cả ba vai trò.
- Lượt chạy test case thứ nhất: 190 test case, 185 Pass, 5 Blocked, 0 Fail.
- Đã kiểm tra trường hợp hai phiên cùng xử lý một đơn xin nghỉ hoặc yêu cầu đổi ca. Hệ thống chặn thao tác sử dụng dữ liệu cũ, không ghi đè kết quả đã lưu và hiển thị thông báo để người dùng tải lại dữ liệu.

## 4. Đối chiếu yêu cầu Tuần 08

| Nhóm | Kết quả | Ghi chú |
| --- | --- | --- |
| PF-01 đến PF-05 | Đạt qua rà soát source | Không phát hiện lỗi thiết kế thuộc nhóm bắt buộc |
| DP-01, DP-03, DP-04, DP-05 | Đạt | README, migration và dữ liệu demo đã được kiểm tra |
| DP-02 | Chưa thực hiện | Tiêu chí khuyến nghị |
| TE-02 | Đạt ở mức test nghiệp vụ | Các chức năng Tuần 08 có test tương ứng |
| Kiểm thử tích hợp | Đã chạy thử trên database mới | Chưa có test HTTP tự động trong xUnit |
| PF-06 | Chưa thực hiện | Tiêu chí khuyến nghị |

## 5. Công việc chưa hoàn thiện

- Chưa có test HTTP tự động chạy trong xUnit.
- Còn 5 test case về xử lý đồng thời cần cập nhật kết quả mong đợi cho phù hợp với giao diện Razor MVC, sau đó chạy lại.
- Chưa đo thời gian phản hồi của ba chức năng theo PF-06.
- Chưa có `.gitattributes` để đồng nhất LF giữa Windows và CI.
- Báo cáo Word chưa cập nhật theo source Tuần 08.
- QT-2/NC-02 chưa có lịch rảnh lặp theo tuần; NC-09 chưa có vai Trưởng ca; NC-10 chưa có Email/SMS.
- BR-01 chưa hỗ trợ thay đổi số lượng ca trong ngày; BR-10 chưa có giới hạn số ca đêm liên tiếp.

Theo nội dung đã trao đổi, BR-09 về ngày lễ và QT-3/NC-11 về nhân bản kỳ lịch dự kiến được chuyển sang phần cải tiến. Em xin GVHD xác nhận lại phạm vi này.

## 6. Nội dung cần GVHD xác nhận

1. Em đã chạy thử hệ thống với một database mới và đăng nhập bằng đủ ba vai trò. Thầy cho em hỏi kết quả này đã được tính là kiểm thử tích hợp của Tuần 08 chưa, hay em cần viết thêm test tự động trong xUnit?
2. Đối với BR-01, hệ thống hiện cho phép Quản lý chỉnh giờ bắt đầu và kết thúc của ba ca. Thầy cho em hỏi có cần cho phép thêm hoặc xóa ca để thay đổi số lượng ca trong ngày không?
3. Đối với BR-10, ca tối hiện tại có thời gian từ 16:00 đến 20:00. Thầy cho em hỏi ca này có được tính là ca đêm không? Nếu có thì một nhân viên được làm liên tiếp tối đa bao nhiêu ca tối?
4. Hệ thống hiện có hộp thư thông báo trong giao diện. Thầy cho em hỏi có bắt buộc phải bổ sung thêm Email hoặc SMS trong phạm vi nghiệm thu không?
5. Xin thầy xác nhận chức năng quản lý ngày lễ và nhân bản kỳ lịch trước được phép chuyển sang phần cải tiến, không bắt buộc triển khai trong phiên bản nghiệm thu hiện tại.

## 7. Kế hoạch tiếp theo

- Tiếp thu phản hồi của GVHD.
- Hoàn thiện các nội dung bắt buộc còn thiếu.
- Đồng bộ báo cáo Word và tài liệu kiểm thử.
- Hoàn tất Pull Request Tuần 08.
