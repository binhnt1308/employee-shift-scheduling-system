namespace ShiftScheduling.Models.Constants;

// Tập trung tên vai trò dùng cho phân quyền để tránh rải chuỗi quyền trong Controller và View.
public static class RoleNames
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
    public const string AdminOrManager = Admin + "," + Manager;
    public const string ManagerOrEmployee = Manager + "," + Employee;
}
