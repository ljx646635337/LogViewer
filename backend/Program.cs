using Microsoft.EntityFrameworkCore;
using LogViewer.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// ===== 0. 飞书通知服务 =====
builder.Services.AddScoped<LogViewer.Api.Services.FeishuNotifier>();

// ===== 1. 数据库配置 =====
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=log_viewer;User=root;Password=;";

// MySQL (Pomelo EF Core Provider)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ===== 2. MVC + Razor（开发环境支持修改视图不重启）=====
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// ===== 3. Swagger（仅开发环境）=====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "LogViewer API", Version = "v1" });
});

// ===== 4. CORS =====
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// ===== 5. 路径基地址（支持 /logViewer 子路径部署）=====
var pathBase = app.Configuration["App:PathBase"] ?? "";
app.UsePathBase("/" + pathBase.TrimStart('/'));

// ===== 5.1 静态文件（支持 /logViewer/css/app.css）=====
app.UseStaticFiles();

// ===== 6. 中间件顺序 =====
// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "LogViewer API v1"));
}

app.UseCors();

// MVC + API 路由（统一加 pathBase 前缀）
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=LogViewer}/{action=Index}/{id?}"
);

// ===== 7. 自动建表 =====
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.EnsureCreated();
        Console.WriteLine("[Init] Database ensured.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Init] Database init failed: {ex.Message}");
    }
}

Console.WriteLine($"[LogViewer] Starting on http://localhost:5001{pathBase} (base: {pathBase})");
app.Run("http://localhost:5001");
