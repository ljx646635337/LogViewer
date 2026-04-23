using Microsoft.AspNetCore.Mvc;
using LogViewer.Api.Services;

namespace LogViewer.Api.Controllers;

/// <summary>
/// 错误日志查看器页面
/// 路由: /logViewer/errors
/// </summary>
[Route("logViewer/errors")]
public class ErrorsController : Controller
{
    private readonly LogCacheService _cache;

    public ErrorsController(LogCacheService cache)
    {
        _cache = cache;
    }

    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// 刷新缓存
    /// GET /logViewer/errors/refresh
    /// </summary>
    [HttpGet("refresh")]
    public async Task<JsonResult> Refresh()
    {
        var (success, cooldownRemaining) = await _cache.RefreshAsync();

        if (!success)
        {
            return Json(new { ok = false, error = $"冷却中，请等待 {cooldownRemaining:F1} 秒后再试" });
        }

        return Json(new { ok = true });
    }

    /// <summary>
    /// 获取冷却状态
    /// GET /logViewer/errors/cooldown
    /// </summary>
    [HttpGet("cooldown")]
    public JsonResult GetCooldown()
    {
        var remaining = _cache.GetCooldownRemainingSeconds();
        return Json(new { cooldownRemaining = remaining });
    }
}
