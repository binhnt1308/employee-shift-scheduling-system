## Tuan 10 - Hoàn thiện NC-10, dữ liệu demo và rà soát bản bàn giao

### 1. Đã làm gì

- [x] Rà soát và hoàn thiện NC-10 cho các luồng thông báo chính.
  - Thông báo nội bộ khi lịch được công bố.
  - Thông báo kết quả duyệt/từ chối đơn xin nghỉ.
  - Thông báo trong luồng xử lý yêu cầu chuyển ca.
  - Hỗ trợ gửi email sau khi nghiệp vụ chính đã được lưu.
  - Trường hợp gửi email lỗi không làm rollback kết quả nghiệp vụ.
  - Rà soát việc không tạo lặp notification/email khi thao tác đã hoàn tất.

- [x] Rà soát phần báo cáo và mức độ phân bổ giờ làm.
  - Tính riêng nhóm Full-time và Part-time.
  - Theo dõi số giờ thấp nhất, cao nhất và mức chênh lệch trong từng nhóm.
  - Nhân viên thuộc phạm vi báo cáo nhưng chưa có ca vẫn được tính 0 giờ khi xác định spread.

- [x] Điều chỉnh màn hình lịch của nhân viên.
  - Khi có cả lịch hiện tại và lịch tương lai, ưu tiên kỳ bao phủ ngày hiện tại.
  - Hiển thị đúng ShiftType theo phiên bản cấu hình gắn với SchedulePeriod.

- [x] Hoàn thiện dữ liệu demo cho fresh database.
  - Seeder tạo 8 nhân viên demo.
  - Tách dữ liệu thành 3 kỳ:
    - Demo đang đăng ký.
    - Demo sẵn sàng xếp lịch.
    - Demo đã công bố.
  - Kỳ đang đăng ký dùng để demo Availability và mức ưu tiên Normal/Preferred.
  - Kỳ sẵn sàng xếp lịch dùng để demo Manual Assignment, OR-Tools, Draft và Publish.
  - Kỳ đã công bố có dữ liệu lịch làm việc, báo cáo và điều chỉnh giờ làm thực tế.
  - Điều chỉnh Seeder để không tạo trùng hoặc báo lỗi khi người dùng đã thay đổi dữ liệu demo hợp lệ.
  - Kiểm tra restart ứng dụng sau khi thao tác trên dữ liệu demo.

- [x] Rà soát và cập nhật tài liệu.
  - README đã cập nhật theo bộ dữ liệu demo 3 kỳ.
  - Chương 4 rà lại phần cài đặt, migration, dữ liệu demo và triển khai.
  - Chương 5 cập nhật kết quả test, smoke test, defect và ma trận truy vết.
  - Không ghi HTTP status cho testcase không thực sự chạy qua HTTP pipeline.

- [x] Đã triển khai thử nghiệm hệ thống lên Azure Container Apps qua HTTPS.

Link thử nghiệm:

https://shift-scheduling-web.braveisland-f86e5e3f.eastasia.azurecontainerapps.io/Account/Login

Bản Azure hiện tại được dùng để kiểm tra khả năng chạy hệ thống ngoài môi trường local. Source đã tiếp tục được cập nhật sau lần deploy gần nhất nên chưa xem đây là bản bàn giao cuối.

---

### 2. Cách kiểm thử

#### B1. Regression theo cấu hình tương đương CI

```powershell
dotnet test ".\src\backend\ShiftScheduling.sln" `
  --configuration Release `
  --no-build `
  --filter "FullyQualifiedName!~ControllerDatabaseIntegrationTests" `
  --verbosity normal
```

Kết quả:

```text
Total: 477
Passed: 477
Failed: 0
Skipped: 0
```

Expected: toàn bộ test trong phạm vi CI-equivalent Pass.

Actual: **477/477 Pass**.

#### B2. Kiểm thử riêng DemoDataSeeder

```text
43/43 Pass
```

#### B3. Fresh database smoke test

Đã kiểm tra trên database mới:

```text
Fresh database
→ Seeder tạo đúng 3 kỳ demo và 8 nhân viên
→ Manual Assignment thành công
→ OR-Tools trả về OPTIMAL
→ Employee lưu Normal/Preferred thành công
→ Restart application
→ Seeder không tạo trùng dữ liệu
```

Kết quả:

```text
6/6 bước Pass
```

#### B4. Full suite có ControllerDatabaseIntegrationTests

Khi chạy toàn bộ suite nhưng chưa cấu hình database integration:

```text
Total: 487
Passed: 477
Failed: 10
Skipped: 0
```

10 lỗi phát sinh tại quá trình khởi tạo/dọn dẹp `SqlServerIntegrationFixture` do môi trường chưa có `SHIFT_SCHEDULING_TEST_CONNECTION`. Các test này chưa đi đến bước thực thi nghiệp vụ nên chưa được xem là lỗi chức năng.

---

### 3. Chưa xong / còn vướng

- [ ] Cấu hình lại môi trường cho `ControllerDatabaseIntegrationTests` và chạy lại nhóm integration test.
- [ ] Redeploy Azure từ source cuối cùng.
- [ ] Smoke test lại các luồng chính trên bản Azure mới nhất.
- [ ] Hoàn thiện ảnh bằng chứng chạy test, CI và deployment cho Chương 4 và Chương 5.
- [ ] Rà lần cuối README và báo cáo trước khi bàn giao.

NC-11 Clone kỳ trước và Holiday/BR-09 không tiếp tục triển khai theo phạm vi đã thống nhất với GVHD.

---

### 4. Câu hỏi cho thầy

1. Với phần kiểm thử, em đang tách test nghiệp vụ ở Service/Integration khỏi HTTP/E2E. Trong báo cáo em chỉ ghi HTTP status cho testcase thực sự đi qua HTTP pipeline, cách trình bày này có phù hợp không ạ?

2. Hệ thống đã deploy thử nghiệm lên Azure Container Apps. Em dự kiến chỉ chốt link deploy chính thức sau khi redeploy source cuối cùng và smoke test lại, cách bàn giao như vậy có ổn không ạ?

3. Fresh database hiện có ba kỳ demo tương ứng với ba giai đoạn: đang đăng ký, sẵn sàng xếp lịch và đã công bố. Em sử dụng ba kỳ này để trình diễn các luồng khác nhau của hệ thống, cách tổ chức dữ liệu demo như vậy có phù hợp không ạ?

---

### 5. Tự kiểm - Mục 22

- [x] README có hướng dẫn chạy nhanh và dữ liệu demo.
- [x] Không commit SMTP password, App Password hoặc secret production.
- [x] `git diff --check` không có lỗi whitespace.
- [x] Các thay đổi Seeder có test tương ứng.
- [x] Regression CI-equivalent đạt 477/477 Pass.
- [x] DemoDataSeeder đạt 43/43 Pass.
- [x] Fresh database smoke test đạt 6/6 bước.
- [x] Seeder tạo đúng 3 kỳ demo và 8 nhân viên.
- [x] Branch `tuan-10` đã được push với commit `2e44d87`.
- [ ] Integration test cần môi trường SQL Server test riêng để chạy lại đầy đủ.
- [ ] Azure cần redeploy từ source cuối cùng trước khi bàn giao.
