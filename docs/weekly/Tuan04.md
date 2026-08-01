# NHẬT KÝ TUẦN 04
Nguyễn Thanh Bình-2354050017
HỆ THỐNG LẬP LỊCH VÀ PHÂN CÔNG CA LÀM VIỆC
## 1. Đã làm

- Chỉnh sửa Chương 3 theo nhận xét của thầy :
  - Bổ sung mục 3.12 – Thiết kế kiến trúc hệ thống.
  - Sửa Class Diagram, loại bỏ lớp SchedulePeriod bị trùng và chuẩn hóa các trạng thái bằng enum.
  - Bổ sung khóa chính, khóa ngoại và bội số quan hệ cho ERD.
  - Bổ sung Activity Diagram và Sequence Diagram cho UC07, UC09 và UC10.
  - Rà soát lại thứ tự đặc tả Use Case, số hiệu hình, bảng và trích dẫn tài liệu tham khảo.
- Khởi tạo project ShiftScheduling bằng ASP.NET Core MVC trên .NET 8.
- Xây dựng cấu trúc thư mục Controllers, Services, Models, ViewModels, Data, Views và Migrations.
- Xây dựng 11 Entity theo ERD và cấu hình ApplicationDbContext.
- Tạo Migration và cơ sở dữ liệu ShiftSchedulingDb trên SQL Server.
- Hoàn thành các chức năng ban đầu:
  - Đăng nhập và đăng xuất.
  - Quản lý tài khoản.
  - Quản lý nhân viên.
  - Quản lý ca làm việc.
- Bắt đầu viết Chương 4 – Cài đặt và triển khai hệ thống.

## 2. Khó khăn trong quá trình làm

- Một số giao diện cũ sử dụng tên Controller, Model và thuộc tính bằng tiếng Việt nên cần cập nhật lại để phù hợp với backend đã chuẩn hóa sang tiếng Anh.
- Cần tiếp tục kiểm tra tính nhất quán giữa Class Diagram, ERD, Sequence Diagram và mã nguồn.
- Một số sơ đồ khi chèn vào Word còn nhỏ, cần xuất lại ảnh có độ phân giải cao.
- Các chức năng liên quan đến lập lịch có nhiều ràng buộc nên cần triển khai và kiểm thử theo từng bước.
- Lỗi version khi cài đặt các package.

## 3. Kế hoạch tuần tới

- Hoàn thiện chức năng quản lý kỳ lịch.
- Xây dựng chức năng thiết lập định mức nhân sự.
- Xây dựng chức năng đăng ký lịch rảnh.
- Bắt đầu chức năng phân công nhân viên thủ công.
- Xây dựng Service kiểm tra các ràng buộc phân ca.
- Tiếp tục hoàn thiện nội dung và hình ảnh cho Chương 4.
- Kiểm thử các chức năng đã hoàn thành và cập nhật mã nguồn lên GitHub.
