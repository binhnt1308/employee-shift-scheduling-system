using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Email;
using ShiftScheduling.Common.Notifications;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Features.Accounts.Services;
using ShiftScheduling.Features.Authentication.Repositories;
using ShiftScheduling.Features.Authentication.Services;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.Employees.Services;
using ShiftScheduling.Features.LeaveRequests.Repositories;
using ShiftScheduling.Features.LeaveRequests.Services;
using ShiftScheduling.Features.Notifications.Services;
using ShiftScheduling.Features.ShiftTransfers.Repositories;
using ShiftScheduling.Features.ShiftTransfers.Services;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Features.ShiftTypes.Services;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Infrastructure.Persistence.Seed;
using ShiftScheduling.Models.Errors;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:DefaultConnection bằng User Secrets hoặc biến môi trường.");
}

var vietnamCulture = new CultureInfo("vi-VN");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(vietnamCulture);
    options.SupportedCultures = new[] { vietnamCulture };
    options.SupportedUICultures = new[] { vietnamCulture };
});

builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddScoped<IUnitOfWork, ApplicationUnitOfWork>();
builder.Services.AddScoped<IClock, SystemClock>();
builder.Services.AddScoped<IPasswordHashService, Pbkdf2PasswordHashService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection(SmtpSettings.SectionName));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
builder.Services.AddScoped<ILeaveRequestService, LeaveRequestService>();
builder.Services.AddScoped<IShiftTransferRepository, ShiftTransferRepository>();
builder.Services.AddScoped<IShiftTransferService, ShiftTransferService>();
builder.Services.AddScoped<IShiftTypeRepository, ShiftTypeRepository>();
builder.Services.AddScoped<IShiftTypeService, ShiftTypeService>();
builder.Services.AddScoped<
    ShiftScheduling.Features.Dashboard.Services.IDashboardService,
    ShiftScheduling.Features.Dashboard.Services.DashboardService>();
builder.Services.AddScoped<
    ShiftScheduling.Features.SchedulePeriods.Repositories.ISchedulePeriodRepository,
    ShiftScheduling.Features.SchedulePeriods.Repositories.SchedulePeriodRepository>();
builder.Services.AddScoped<
    ShiftScheduling.Features.SchedulePeriods.Services.ISchedulePeriodService,
    ShiftScheduling.Features.SchedulePeriods.Services.SchedulePeriodService>();

builder.Services.AddScoped<
    ShiftScheduling.Features.Availabilities.Repositories.IAvailabilityRepository,
    ShiftScheduling.Features.Availabilities.Repositories.AvailabilityRepository>();
builder.Services.AddScoped<
    ShiftScheduling.Features.Availabilities.Repositories.IWeeklyAvailabilityPatternRepository,
    ShiftScheduling.Features.Availabilities.Repositories.WeeklyAvailabilityPatternRepository>();
builder.Services.AddScoped<
    ShiftScheduling.Features.Availabilities.Services.IAvailabilityService,
    ShiftScheduling.Features.Availabilities.Services.AvailabilityService>();

builder.Services.AddScoped<
    ShiftScheduling.Features.WorkSchedules.Services.ISchedulingValidationService,
    ShiftScheduling.Features.WorkSchedules.Services.SchedulingValidationService>();
builder.Services.AddScoped<
    ShiftScheduling.Features.WorkSchedules.Services.IOrToolsSchedulingService,
    ShiftScheduling.Features.WorkSchedules.Services.OrToolsSchedulingService>();
builder.Services.AddScoped<
    ShiftScheduling.Features.WorkSchedules.Repositories.IWorkScheduleRepository,
    ShiftScheduling.Features.WorkSchedules.Repositories.WorkScheduleRepository>();
builder.Services.AddScoped<
    ShiftScheduling.Features.WorkSchedules.Services.IWorkScheduleService,
    ShiftScheduling.Features.WorkSchedules.Services.WorkScheduleService>();

builder.Services.AddScoped<
    ShiftScheduling.Features.Reports.Services.IWorkReportService,
    ShiftScheduling.Features.Reports.Services.WorkReportService>();
builder.Services.AddScoped<INotificationService, NotificationService>();


builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsync(
            "Bạn đã thử đăng nhập quá nhiều lần. Vui lòng chờ một lát rồi thử lại.",
            token);
    };

    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    // Chỉ bắt buộc HTTPS trên môi trường triển khai.
    app.UseHttpsRedirection();
    app.UseHsts();
}
if (app.Environment.IsDevelopment())
{
    // Hiện chi tiết lỗi trong môi trường phát triển để dễ sửa.
    app.UseDeveloperExceptionPage();
}
else
{
    // Ẩn thông tin lỗi nội bộ khi chạy trên môi trường triển khai.
    app.UseExceptionHandler("/Home/Error");
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseRequestLocalization();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    var demoDataEnabled = app.Configuration.GetValue<bool?>("DemoData:Enabled") ?? app.Environment.IsDevelopment();
    if (demoDataEnabled)
    {
        var passwordHashService = scope.ServiceProvider.GetRequiredService<IPasswordHashService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        await DemoDataSeeder.SeedAsync(dbContext, passwordHashService, clock);
    }
}

await app.RunAsync();

public partial class Program { }
