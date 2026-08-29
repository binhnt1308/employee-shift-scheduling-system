# BRD - Business Rule Mapping

| BRD | Nội dung chính | BR trong báo cáo/source | Tình trạng |
|---|---|---|---|
| BR-01 | Số ca tối đa mỗi ngày do đơn vị cấu hình, không trùng ca | BR08, BR09, BR10 | Đã có một phần, hiện quy tắc số ca/ngày vẫn cố định theo PT/FT |
| BR-02 | Chỉ phân ca khi nhân viên đã đăng ký rảnh và không có đơn nghỉ được duyệt | BR05, BR06 | Đã có |
| BR-03 | Nghỉ tối thiểu giữa ca cuối ngày và ca đầu ngày kế tiếp | BR20 | Đã thêm vào báo cáo|
| BR-04 | Ca phải đủ nhân sự theo vị trí và năng lực phù hợp | BR07, BR12, BR13 | Đã có |
| BR-05 | Giới hạn tổng giờ làm mỗi tuần theo loại hợp đồng | BR09, BR10, BR11, BR18 | Đã có; FullTime tối đa 48 giờ/tuần, PartTime tối đa 28 giờ/tuần (mục tiêu mềm 20 giờ/tuần), tính từ tổng DurationHours thực tế; áp dụng đầy đủ tại CP-SAT solver, phân công/thay ca thủ công, duyệt công bố và đổi ca |
| BR-06 | Lịch chỉ có hiệu lực sau khi Quản lý công bố | BR14, BR15, BR16 | Đã có |
| BR-07 | Đổi ca cần người nhận đồng ý, Quản lý duyệt và không được vi phạm các quy tắc phân ca | BR17 và các BR kiểm tra phân ca | Đã có |
| BR-08 | Nhân viên phải có ít nhất 24 giờ nghỉ liên tục mỗi tuần | BR21 | Đã thêm vào báo cáo|
| BR-09 | Nhận diện ngày lễ và quy định việc xếp lịch vào ngày lễ | Chưa có | Chưa có |
| BR-10 | Ưu tiên cân bằng lịch và các nguyện vọng khi có nhiều phương án hợp lệ | BR18, BR19 | Đã có trong phạm vi của đồ án |
