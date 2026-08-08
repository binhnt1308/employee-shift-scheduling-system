# BÁO CÁO TIẾN ĐỘ

## 1. Công việc đã thực hiện

Trong giai đoạn này, hệ thống tiếp tục được hoàn thiện các chức năng liên quan đến
lập lịch làm việc và quản lý nhân sự.

Các nội dung chính đã thực hiện:

- Hoàn thiện chức năng đăng ký lịch rảnh của nhân viên.
- Hoàn thiện chức năng phân công và điều chỉnh lịch làm việc.
- Bổ sung kiểm tra các quy tắc nghiệp vụ khi phân công nhân viên.
- Tích hợp Google OR-Tools CP-SAT để hỗ trợ sinh lịch đề xuất.
- Hoàn thiện chức năng kiểm tra và công bố lịch làm việc.
- Hoàn thiện chức năng xem lịch làm việc đã công bố của nhân viên.
- Hoàn thiện chức năng gửi và duyệt đơn xin nghỉ.
- Hoàn thiện quy trình chuyển ca giữa các nhân viên.
- Hoàn thiện chức năng tổng hợp số ca, số ngày và số giờ làm việc.
- Bổ sung kiểm thử tự động cho các chức năng và quy tắc nghiệp vụ chính.
- Cập nhật tài liệu test case và bảng bao phủ Business Rule.
- Cập nhật README theo trạng thái hiện tại của hệ thống.
- Bổ sung cấu hình `.gitignore` để loại trừ cấu hình phát triển cục bộ.

## 2. Kiểm thử

Bộ kiểm thử tự động đã được chạy cho các chức năng đã triển khai.

Kết quả kiểm thử gần nhất:

- Build: thành công.
- Automated tests: 85/85 test Pass.
- Không có test Fail.

Các nhóm đã được kiểm tra gồm:

- Đăng nhập và phân quyền.
- Quản lý nhân viên.
- Kỳ lịch và định mức.
- Đăng ký lịch rảnh.
- Phân công lịch.
- Các ràng buộc Part-time và Full-time.
- Sinh lịch bằng OR-Tools.
- Đơn xin nghỉ.
- Chuyển ca.
- Báo cáo số ca và giờ làm.

Ngoài kiểm thử tự động, các luồng chính như phân quyền, công bố lịch,
kiểm tra dữ liệu thay đổi trước khi công bố và xuất báo cáo cũng đã được kiểm tra
trên ứng dụng.

## 3. Kết quả đạt được

Luồng lập lịch hiện tại:

`Tạo kỳ lịch → Đăng ký lịch rảnh → Phân công thủ công hoặc sinh lịch đề xuất
→ Kiểm tra lịch → Công bố → Nhân viên xem lịch`( chưa có vì chưa tạo dữ liệu nhân viên đăng ký ca )

Các chức năng chính từ UC01 đến UC11 đã có phần triển khai tương ứng trong hệ thống.

Kiến trúc tiếp tục tuân theo hướng:

`Controller → Service → Repository`

Controller không truy cập trực tiếp DbContext.

Các quy tắc nghiệp vụ quan trọng được kiểm tra tại tầng Service trước khi dữ liệu
được cập nhật.

## 4. Công việc còn lại

- Tiếp tục rà soát test cho các Business Rule còn thiếu trường hợp kiểm thử riêng.
- Hoàn thiện tài liệu kiểm thử và ma trận truy vết theo yêu cầu.
- Tiếp tục rà soát comment và tài liệu XML cho các Service.
- Cấu hình CI để tự động build và chạy test khi push hoặc tạo Pull Request.
- Tiếp tục hoàn thiện báo cáo và giao diện khi cần thiết.

## 5. Khó khăn 

- Project sử dụng .NET 8 nên khi cài thêm package phải kiểm tra phiên bản tương thích.
  Nếu dùng package hoặc ví dụ của phiên bản khác có thể phát sinh lỗi khi restore hoặc build.

- Việc tích hợp Google OR-Tools khá khó vì phải chuyển các quy tắc nghiệp vụ
  thành các ràng buộc để solver xử lý.

- Các quy tắc lập lịch liên quan đồng thời đến lịch rảnh, đơn nghỉ, vị trí,
  level và loại nhân viên nên cần kiểm tra dữ liệu cẩn thận.

- Trước khi công bố hoặc cập nhật lịch, hệ thống cần đọc lại dữ liệu mới nhất
  để tránh dùng dữ liệu đã thay đổi.

- Một số thay đổi ở Service có thể ảnh hưởng nhiều chức năng khác,
  nên cần chạy lại build và toàn bộ test sau mỗi nhóm thay đổi.
