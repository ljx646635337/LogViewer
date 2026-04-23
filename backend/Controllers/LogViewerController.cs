using Microsoft.AspNetCore.Mvc;
using LogViewer.Api.Data;
using Microsoft.EntityFrameworkCore;
using LogViewer.Api.Services;

namespace LogViewer.Api.Controllers;

public class LogViewerController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogViewerController> _logger;
    private readonly LogCacheService _cache;

    public LogViewerController(AppDbContext db, ILogger<LogViewerController> logger, LogCacheService cache)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        // 从缓存获取统计数据
        var (todayErrorCount, totalCount, lastLogTime) = await _cache.GetStatsAsync();

        ViewData["TodayErrorCount"] = todayErrorCount;
        ViewData["TotalCount"] = totalCount;
        ViewData["LastLogTime"] = lastLogTime > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(lastLogTime).ToString("yyyy-MM-dd HH:mm:ss")
            : "—";

        return View();
    }
}
