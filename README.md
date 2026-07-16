# Employee Shift Scheduling & Workforce Management System

## Tên đề tài

**Tên:** Employee Shift Scheduling & Workforce Management System (Hệ thống lập lịch và quản lý phân công ca làm việc cho nhân viên)

## Giới thiệu đề tài

Đề tài xây dựng một hệ thống web hỗ trợ lập lịch và quản lý phân công ca làm việc cho nhân viên tại một cửa hàng bán lẻ quy mô nhỏ. Hệ thống được áp dụng cho một chi nhánh có khoảng 10–20 nhân viên, gồm hai vị trí công việc là nhân viên tư vấn và nhân viên thu ngân.

Hệ thống cho phép nhân viên đăng ký lịch rảnh, xem lịch làm việc, gửi đơn xin nghỉ và yêu cầu đổi ca. Quản lý có thể quản lý thông tin nhân viên, thiết lập nhu cầu nhân sự, phân công ca, duyệt các yêu cầu và công bố lịch làm việc chính thức.

Nhân viên được phân loại theo hình thức làm việc toàn thời gian hoặc bán thời gian và theo ba cấp độ năng lực gồm Level 1, Level 2 và Level 3. Khi phân ca, hệ thống kiểm tra các điều kiện như lịch rảnh, ngày nghỉ, vị trí công việc, hình thức làm việc, level và nhu cầu nhân sự của từng ca.

Ngoài chức năng phân ca thủ công, đề tài dự kiến sử dụng Google OR-Tools CP-SAT để hỗ trợ tạo lịch đề xuất. Lịch đề xuất chỉ là phương án tham khảo; quản lý vẫn là người kiểm tra, điều chỉnh và công bố lịch cuối cùng.
## Chức năng chính

- Quản lý tài khoản và phân quyền.
- Quản lý thông tin nhân viên.
- Quản lý vị trí, level và hình thức làm việc.
- Đăng ký lịch rảnh theo tuần.
- Phân công ca thủ công.
- Tạo lịch đề xuất bằng Google OR-Tools CP-SAT.
- Quản lý đơn xin nghỉ.
- Quản lý yêu cầu đổi ca.
- Công bố lịch làm việc.
- Tổng hợp số ca và số giờ làm.
