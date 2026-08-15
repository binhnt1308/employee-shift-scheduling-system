# Business Rule Test Coverage - Tuần 5

Mục tiêu: kiểm tra mỗi quy tắc nghiệp vụ đã triển khai đều có ít nhất một test hợp lệ
và một test vi phạm.

| BR   | Quy tắc                                                                  | Test đúng | Test sai | Hiện tại                                                                                                   |
| ---- | ------------------------------------------------------------------------ | :-------: | :------: | ---------------------------------------------------------------------------------------------------------- |
| BR01 | Phân quyền                                                               | Có        | Có       | Đã đủ test                                                                                                 |
| BR02 | Nhân viên hợp lệ và đang Active mới được xếp lịch                        | Chưa rõ   | Chưa rõ  | Cần bổ sung test nhân viên Active được xếp lịch và nhân viên Inactive không được xếp lịch                  |
| BR03 | Kỳ lịch gồm 7 ngày và 3 ca cố định                                       | Có        | Có       | Đã đủ test                                                                                                 |
| BR04 | Chỉ được đăng ký lịch rảnh khi kỳ đăng ký đang Open và chưa quá deadline | Có        | Có       | Đã đủ test                                                                                                 |
| BR05 | Nhân viên phải đăng ký rảnh mới được xếp ca                              | Chưa rõ   | Có       | Cần bổ sung test nhân viên đã đăng ký rảnh được xếp ca thành công                                          |
| BR06 | Nhân viên có đơn nghỉ Approved không được xếp ca                         | Chưa rõ   | Có       | Cần bổ sung test nhân viên không có đơn nghỉ Approved được xếp ca thành công                               |
| BR07 | Nhân viên phải được xếp đúng vị trí                                      | Chưa rõ   | Có       | Cần bổ sung test nhân viên đúng vị trí được xếp ca thành công                                              |
| BR08 | Nhân viên không được xếp vào hai ca trùng thời gian                      | Chưa rõ   | Có       | Cần bổ sung test hai ca không trùng thời gian được chấp nhận                                               |
| BR09 | Nhân viên Part-time được làm tối đa 1 ca trong ngày                      | Chưa rõ   | Có       | Cần bổ sung test nhân viên Part-time làm đúng 1 ca trong ngày được chấp nhận                               |
| BR10 | Nhân viên Full-time làm 2 ca liên tiếp                                   | Chưa có   | Có       | Cần bổ sung test hai trường hợp hợp lệ là Sáng–Chiều và Chiều–Tối                                          |
| BR11 | Nhân viên Full-time làm tối đa 6 ngày trong tuần                         | Chưa có   | Có       | Cần bổ sung test nhân viên Full-time làm đúng 6 ngày trong tuần được chấp nhận                             |
| BR12 | Mỗi ca phải đủ định mức nhân sự                                          | Chưa rõ   | Có       | Cần bổ sung test ca đủ định mức nhân sự được xác định là hợp lệ                                            |
| BR13 | Mỗi ca phải có ít nhất một nhân viên Level 2 trở lên                     | Chưa rõ   | Có       | Cần bổ sung test ca có nhân viên Level 2 hoặc Level 3 được xác định là hợp lệ                              |
| BR14 | Lịch Draft có lỗi vẫn được lưu nhưng không được Publish                  | Chưa có   | Chưa có  | Cần bổ sung test lưu Draft khi còn lỗi và test không cho Publish lịch còn lỗi                              |
| BR15 | OR-Tools chỉ tạo lịch Proposal                                           | Chưa rõ   | Có       | Cần bổ sung test xác nhận Generate chỉ tạo Proposal và không tự động Publish                               |
| BR16 | Phải kiểm tra lại dữ liệu mới nhất trước khi cập nhật                    | Có        | Có       | Đã đủ test                                                                                                 |
| BR17 | Đổi ca theo hình thức một chiều và đúng quy trình                        | Có        | Có       | Đã đủ test                                                                                                 |
| BR18 | Part-time khoảng 5 ca và Full-time khoảng 6 ngày là cảnh báo mềm         | Chưa có   | Chưa có  | Cần bổ sung test đạt mức thì không cảnh báo và chưa đạt mức thì chỉ cảnh báo, không phát sinh lỗi bắt buộc |
| BR19 | Cân bằng khối lượng làm việc là mục tiêu mềm                             | Chưa có   | Chưa có  | Cần bổ sung test kiểm tra mức độ cân bằng số ca hoặc số giờ giữa các nhân viên cùng loại hình làm việc     |
| BR20 | Nhân viên phải có ít nhất 12 giờ nghỉ liên tục giữa ca cuối của ngày trước và ca đầu của ngày kế tiếp | Có | Có | Đã có test đúng 12 giờ được chấp nhận và test dưới 12 giờ bị từ chối |
| BR21 | Trong mỗi kỳ 7 ngày, nhân viên phải có ít nhất một khoảng nghỉ liên tục từ 24 giờ trở lên | Có | Có | Đã có test khoảng nghỉ đủ 24 giờ được chấp nhận và test làm liên tục không có đủ 24 giờ nghỉ bị từ chối |

## Kết luận rà soát

Các Business Rule đã đủ cả test hợp lệ và test vi phạm:

- BR01
- BR03
- BR04
- BR16
- BR17
- BR20
- BR21

Các Business Rule cần bổ sung hoặc xác minh thêm test:

- BR02
- BR05
- BR06
- BR07
- BR08
- BR09
- BR10
- BR11
- BR12
- BR13
- BR14
- BR15
- BR18
- BR19

Sau khi bổ sung, chạy toàn bộ test bằng một lệnh:

```bash
dotnet test src/backend/ShiftScheduling.sln --verbosity normal
