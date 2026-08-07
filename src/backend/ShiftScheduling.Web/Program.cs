using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ShiftScheduling.Common.Auditing;
using ShiftScheduling.Common.Security;
using ShiftScheduling.Common.Time;
using ShiftScheduling.Features.Accounts.Repositories;
using ShiftScheduling.Features.Accounts.Services;
using ShiftScheduling.Features.Authentication.Repositories;
using ShiftScheduling.Features.Authentication.Services;
using ShiftScheduling.Features.Employees.Repositories;
using ShiftScheduling.Features.Employees.Services;
using ShiftScheduling.Features.ShiftTypes.Repositories;
using ShiftScheduling.Features.ShiftTypes.Services;
using ShiftScheduling.Infrastructure.Persistence;
using ShiftScheduling.Models.Errors;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Chưa cấu hình ConnectionStrings:DefaultConnection bằng User Secrets hoặc biến môi trường.");
}

builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddScoped<IUnitOfWork, ApplicationUnitOfWork>();
builder.Services.AddScoped<IClock, SystemClock>();
builder.Services.AddScoped<IPasswordHashService, Pbkdf2PasswordHashService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

builder.Services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IShiftTypeRepository, ShiftTypeRepository>();
builder.Services.AddScoped<IShiftTypeService, ShiftTypeService>();
builder.Services.AddScoped<ShiftScheduling.Features.Dashboard.Services.IDashboardService, ShiftScheduling.Features.Dashboard.Services.DashboardService>();
builder.Services.AddScoped<ShiftScheduling.Features.SchedulePeriods.Repositories.ISchedulePeriodRepository, ShiftScheduling.Features.SchedulePeriods.Repositories.SchedulePeriodRepository>();
builder.Services.AddScoped<ShiftScheduling.Features.SchedulePeriods.Services.ISchedulePeriodService, ShiftScheduling.Features.SchedulePeriods.Services.SchedulePeriodService>();

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
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    // Chi bat buoc HTTPS  tren moi truong trien khai.
    app.UseHttpsRedirection();
    app.UseHsts();
}
if (app.Environment.IsDevelopment())
{
    // Hien chi tiet loi trong moi truong phat trien de de sua.
    app.UseDeveloperExceptionPage();
}
else
{
    // An thong tin loi noi bo khi chay tren moi truong trien khai.
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

app.UseHttpsRedirection();
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
}

await app.RunAsync();

public partial class Program { }
