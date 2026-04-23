using Microsoft.AspNetCore.Mvc;
using LogViewer.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LogViewer.Api.Controllers;

public class LogViewerController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogViewerController> _logger;

    public LogViewerController(AppDbContext db, ILogger<LogViewerController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        // 今日 error 数（Skynet time_ms 是 UTC 时间戳，需要 +8 转北京时间）
        var todayStartUtc = DateTime.UtcNow.Date;
        var todayStartTs = new DateTimeOffset(todayStartUtc).ToUnixTimeMilliseconds();
        var todayErrorCount = await _db.ErrorLogs
            .Where(l => l.Level == "error" && l.TimeMs >= todayStartTs)
            .CountAsync();

        // 总记录数
        var totalCount = await _db.ErrorLogs.CountAsync();

        // 最后上报时间
        var lastLog = await _db.ErrorLogs
            .OrderByDescending(l => l.TimeMs)
            .Select(l => (long?)l.TimeMs)
            .FirstOrDefaultAsync();

        ViewData["TodayErrorCount"] = todayErrorCount;
        ViewData["TotalCount"] = totalCount;
        ViewData["LastLogTime"] = lastLog > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastLog.Value).ToString("yyyy-MM-dd HH:mm:ss")
            : "—";

        return View();
    }
}
