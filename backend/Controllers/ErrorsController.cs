using Microsoft.AspNetCore.Mvc;

namespace LogViewer.Api.Controllers;

/// <summary>
/// 错误日志查看器页面
/// 路由: /logViewer/errors
/// </summary>
[Route("logViewer/errors")]
public class ErrorsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
