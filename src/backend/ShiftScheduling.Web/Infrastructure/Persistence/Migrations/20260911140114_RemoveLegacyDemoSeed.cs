using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiftScheduling.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyDemoSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Xóa an toàn nhân viên legacy EMP001 có điều kiện cụ thể (EmployeeCode + FullName + Email),
            // tuyệt đối không xóa dựa theo EmployeeId đơn độc để không ảnh hưởng NV01 trên database cũ.
            migrationBuilder.Sql(@"
                DELETE FROM [dbo].[Employees]
                WHERE [EmployeeCode] = 'EMP001'
                  AND [FullName] = N'Nguyễn Văn Nhân Viên'
                  AND [Email] = 'nhanvien@example.com';

                -- Xóa an toàn tài khoản legacy nhanvien chỉ khi không còn bất kỳ Employee nào tham chiếu
                DELETE FROM [dbo].[UserAccounts]
                WHERE [Username] = 'nhanvien'
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[Employees] e WHERE e.[UserAccountId] = [dbo].[UserAccounts].[UserAccountId]
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fail-fast rollback: Nếu ID 1, ID 3 hoặc email đã bị entity khác chiếm giữ thì THROW lỗi rõ ràng,
            // không ghi đè dữ liệu và không silently skip rollback.
            migrationBuilder.Sql(@"
                -- 1. Kiểm tra conflict cho UserAccount: ID 3 bị account khác chiếm
                IF EXISTS (SELECT 1 FROM [dbo].[UserAccounts] WHERE [UserAccountId] = 3 AND [Username] <> 'nhanvien')
                BEGIN
                    THROW 51001, 'Cannot rollback RemoveLegacyDemoSeed: UserAccountId 3 is occupied by another account.', 1;
                END

                -- 2. Kiểm tra conflict cho UserAccount: Username 'nhanvien' bị gán cho ID khác
                IF EXISTS (SELECT 1 FROM [dbo].[UserAccounts] WHERE [Username] = 'nhanvien' AND [UserAccountId] <> 3)
                BEGIN
                    THROW 51002, 'Cannot rollback RemoveLegacyDemoSeed: Username nhanvien is occupied by a different account ID.', 1;
                END

                -- 3. Kiểm tra conflict cho Employee: ID 1 bị nhân viên khác chiếm
                IF EXISTS (SELECT 1 FROM [dbo].[Employees] WHERE [EmployeeId] = 1 AND [EmployeeCode] <> 'EMP001')
                BEGIN
                    THROW 51003, 'Cannot rollback RemoveLegacyDemoSeed: EmployeeId 1 is occupied by another employee.', 1;
                END

                -- 4. Kiểm tra conflict cho Employee: Email nhanvien@example.com bị nhân viên khác chiếm
                IF EXISTS (SELECT 1 FROM [dbo].[Employees] WHERE [Email] = 'nhanvien@example.com' AND [EmployeeCode] <> 'EMP001')
                BEGIN
                    THROW 51004, 'Cannot rollback RemoveLegacyDemoSeed: Email nhanvien@example.com is occupied by another employee.', 1;
                END

                -- 5. Restore UserAccount nhanvien nếu chưa có
                IF NOT EXISTS (SELECT 1 FROM [dbo].[UserAccounts] WHERE [UserAccountId] = 3 AND [Username] = 'nhanvien')
                BEGIN
                    SET IDENTITY_INSERT [dbo].[UserAccounts] ON;
                    INSERT INTO [dbo].[UserAccounts] ([UserAccountId], [CreatedAt], [CreatedByAccountId], [IsLocked], [PasswordHash], [Role], [Username])
                    VALUES (3, '2026-08-01T00:00:00.000Z', 1, 0, '100000.o2UkhUfzacTkDzOTs2NgPQ==.H/cGgTusNfv1PTAS9pTbBKEfNKMSw5IurN0GUEwEh90=', 3, 'nhanvien');
                    SET IDENTITY_INSERT [dbo].[UserAccounts] OFF;
                END

                -- 6. Restore Employee EMP001 nếu chưa có
                IF NOT EXISTS (SELECT 1 FROM [dbo].[Employees] WHERE [EmployeeId] = 1 AND [EmployeeCode] = 'EMP001')
                BEGIN
                    SET IDENTITY_INSERT [dbo].[Employees] ON;
                    INSERT INTO [dbo].[Employees] ([EmployeeId], [DateOfBirth], [Email], [EmployeeCode], [EmploymentType], [FullName], [IsActive], [Level], [PhoneNumber], [Position], [StartDate], [UserAccountId])
                    VALUES (1, '2000-01-01', 'nhanvien@example.com', 'EMP001', 2, N'Nguyễn Văn Nhân Viên', 1, 2, '0901234567', 1, '2026-01-01', 3);
                    SET IDENTITY_INSERT [dbo].[Employees] OFF;
                END
            ");
        }
    }
}
