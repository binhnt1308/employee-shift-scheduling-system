# NHẬT KÝ TUẦN 03

## 1. Đã thực hiện

- Chỉnh sửa Chương 3 theo góp ý của giảng viên ở Tuần 02.
- Tách lại các Use Case theo từng mục tiêu nghiệp vụ:
  - Thiết lập kỳ lịch và định mức ca.
  - Phân công và điều chỉnh lịch làm việc.
  - Sinh lịch đề xuất tự động bằng OR-Tools.
  - Duyệt và công bố lịch.
  - Xem lịch đã công bố.
- Cập nhật lại yêu cầu chức năng thành FR01–FR11 và đặc tả UC01–UC11.
- Bổ sung mục Quy tắc nghiệp vụ trong Chương 3, phân rõ ràng buộc bắt buộc và ràng buộc ưu tiên.
- Chỉnh sửa sơ đồ phân rã chức năng và Use Case Diagram theo danh sách Use Case mới.
- Vẽ Activity Diagram và Sequence Diagram cho UC05 – Phân công và điều chỉnh lịch làm việc và UC06 – Sinh lịch đề xuất tự động.
- Vẽ Class Diagram toàn hệ thống.
- Vẽ mô hình dữ liệu ERD, bổ sung mô tả các thực thể và mối quan hệ.
- Lập ma trận truy vết FR ↔ UC ↔ lớp ↔ bảng dữ liệu ↔ giao diện.
- Thiết kế các giao diện chính của hệ thống.
- Bổ sung chú thích, đánh số và dẫn chiếu các hình, bảng trong nội dung báo cáo.
- Chỉnh sửa hình thức báo cáo theo yêu cầu: giãn dòng 1.5, lề trái 3 cm, các lề còn lại 2 cm.
- Bổ sung bìa, mục lục, danh mục hình, danh mục bảng, danh mục từ viết tắt và tài liệu tham khảo.
- Bổ sung nội dung cho file `.gitignore`.
- Thử xây dựng một số giao diện bằng ASP.NET MVC và Razor View để kiểm tra khả năng triển khai.

## 2. Đang vướng

- Em chưa hiểu rõ mức độ chi tiết cần trình bày ở phần thiết kế kiến trúc hệ thống: chỉ cần vẽ sơ đồ các tầng và thành phần công nghệ, hay cần mô tả chi tiết luồng xử lý giữa các thành phần.
- Đây là lần đầu em vẽ Class Diagram cho toàn hệ thống nên chưa chắc cách xác định lớp, phương thức và quan hệ giữa các lớp đã đúng chuẩn UML và phù hợp để triển khai bằng Entity Framework Code First.
- Cần giảng viên xem lại sự thống nhất giữa đặc tả Use Case, Activity Diagram, Sequence Diagram, Class Diagram, ERD và ma trận truy vết.
- Cần kiểm tra lại bố cục và kích thước các sơ đồ khi đưa vào Word và xuất sang PDF.
- Các giao diện thử nghiệm đã hiển thị được bố cục cơ bản nhưng vẫn còn một số lỗi trình bày và chưa hoàn thiện toàn bộ chức năng.

## 3. Nội dung cần giảng viên góp ý

- Danh sách UC01–UC11 sau khi tách theo góp ý đã hợp lý và đúng phạm vi đề tài chưa.
- Activity Diagram và Sequence Diagram của UC05, UC06 đã thể hiện đúng luồng nghiệp vụ chưa.
- Class Diagram hiện tại đã đúng chuẩn UML và đủ làm cơ sở xây dựng Model trong chương trình chưa.
- ERD, khóa và các mối quan hệ đã phù hợp với đặc tả Use Case và có thể triển khai bằng SQL Server, Entity Framework Code First chưa.
- Ma trận truy vết FR ↔ UC ↔ lớp ↔ bảng dữ liệu ↔ giao diện đã thể hiện đúng sự liên kết giữa các phần chưa.
- Phần kiến trúc hệ thống cần trình bày ở mức độ nào để phù hợp với yêu cầu của đồ án.

## 4. Kế hoạch tuần tới

- Chỉnh sửa các sơ đồ và đặc tả theo góp ý của giảng viên.
- Hoàn thiện sơ đồ kiến trúc hệ thống và phần mô tả luồng hoạt động.
- Khởi tạo và hoàn thiện cấu trúc project ASP.NET MVC.
- Xây dựng các Model theo Class Diagram và ERD.
- Tạo cơ sở dữ liệu bằng Entity Framework Code First và SQL Server.
- Tiếp tục hoàn thiện các giao diện và chức năng quản lý dữ liệu nền.
