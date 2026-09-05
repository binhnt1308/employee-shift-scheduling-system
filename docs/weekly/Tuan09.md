## WEEKLY  – TUẦN 09

### 1. Công việc đã thực hiện

Trong tuần này, em tập trung cập nhật hệ thống theo các góp ý đã trao đổi với giảng viên hướng dẫn và tiếp tục rà soát lại báo cáo đồ án.

Về chức năng, em đã thực hiện các nội dung sau:

* Bổ sung Weekly Availability Pattern để nhân viên có thể lưu và áp dụng lại mẫu lịch rảnh theo tuần.
* Bổ sung mức độ ưu tiên Normal/Preferred khi nhân viên đăng ký ca.
* Điều chỉnh giao diện đăng ký lịch rảnh và phần hiển thị thông tin liên quan cho quản lý.
* Điều chỉnh Dashboard để hiển thị rõ hơn tên kỳ xếp lịch, khoảng thời gian và trạng thái của kỳ.
* Bổ sung chức năng điều chỉnh giờ làm thực tế cho ca đã kết thúc, kèm kiểm tra dữ liệu và lý do điều chỉnh.
* Sử dụng cơ chế AuditLog hiện có để ghi nhận thay đổi liên quan đến giờ làm thực tế.
* Rà soát các phần báo cáo giờ làm có liên quan đến dữ liệu giờ làm thực tế.
* Bổ sung và cập nhật test cho một số chức năng đã thay đổi.
* Bổ sung integration test cho một số luồng chính như tạo kỳ xếp lịch, đăng ký lịch rảnh, nghỉ phép, phân công ca và điều chỉnh giờ làm.

Về tài liệu và báo cáo:

* Đã tiếp tục chỉnh sửa nội dung báo cáo Word để phù hợp hơn với chức năng và source code hiện tại.
* Đã rà soát lại một số phần mô tả yêu cầu, luồng nghiệp vụ và nội dung Use Case.
* Đã điều chỉnh các nội dung không còn phù hợp với phiên bản hệ thống hiện tại.
* Phần hình ảnh, sơ đồ và screenshot chưa được cập nhật đồng bộ theo các nội dung vừa chỉnh sửa.

### 2. Công việc chưa hoàn thành / còn vướng

* NC-10 hiện mới có thông báo bên trong hệ thống, chưa tích hợp gửi thông báo qua email.
* Nhóm integration test cần SQL Server test database riêng và hiện chưa có cấu hình đầy đủ để chạy trên CI.
* Báo cáo Word chưa rà soát xong toàn bộ UC01–UC11.
* Một số Use Case Diagram, Activity/Sequence Diagram, Class Diagram, ERD và screenshot vẫn còn theo phiên bản cũ.
* Cần tiếp tục đối chiếu giữa nội dung đã sửa và các sơ đồ để tránh trường hợp nội dung văn bản và hình ảnh mô tả khác nhau.
* BR-09 – Holiday và NC-11 – Clone kỳ trước hiện không tiếp tục triển khai theo phạm vi đã trao đổi trước và dự kiến đưa vào phần hướng phát triển.

### 3. Nội dung cần thầy giải đáp

* Với NC-10, hệ thống hiện đã có notification nội bộ nhưng chưa gửi email. Em muốn xác nhận phần email có bắt buộc phải hoàn thành trong phạm vi hiện tại hay có thể xem là phần mở rộng.
* Báo cáo hiện đã chỉnh sửa nội dung trước nhưng chưa cập nhật hết sơ đồ. Với các sơ đồ chỉ bị ảnh hưởng một phần, em muốn hỏi có cần vẽ lại toàn bộ hay chỉ cập nhật phần liên quan.

### 4. Mục tiêu tuần tiếp theo

* Tiếp tục xử lý phần gửi email cho NC-10 sau khi xác định rõ phạm vi.
* Hoàn thiện cấu hình cần thiết cho integration test và kiểm tra lại các luồng đã bổ sung.
* Tiếp tục rà soát UC01–UC11 theo chức năng thực tế.
* Cập nhật lại các sơ đồ và screenshot còn theo phiên bản cũ.
* Đối chiếu lại yêu cầu, source code, test case và báo cáo để giảm sai lệch giữa các phần trước khi chốt phiên bản tiếp theo.
