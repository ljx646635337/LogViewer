using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LogViewer.Api.Data;
using LogViewer.Api.Dtos;
using LogViewer.Api.Models;
using LogViewer.Api.Services;
using System.Text.RegularExpressions;

namespace LogViewer.Api.Controllers;

/// <summary>
/// 日志接收与查询 API
/// </summary>
[ApiController]
[Route("api/logs")]
[Produces("application/json")]
public class LogsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<LogsController> _logger;
    private readonly FeishuNotifier _feishu;

    // Traceback 特征模式：从 msg 中提取堆栈信息
    private static readonly Regex[] TracebackPatterns = new[]
    {
        // Python: "Traceback (most recent call last):"
        new Regex(@"Traceback \(most recent call last\):(.+?)(?=\n[A-Z][a-zA-Z]+Error|\nException|\n(?:Error:)|$)", RegexOptions.Singleline),
        // Lua: "stack traceback:"
        new Regex(@"stack traceback:(.+?)(?=    at |\n[\w.]+\s*=|\Z)", RegexOptions.Singleline),
        // C# / .NET: "at " 开头
        new Regex(@"--- End of stack trace from previous location ---\r?\n(.+?)(?=\Z)", RegexOptions.Singleline),
        // 通用 "at " 格式
        new Regex(@"(?:at|in) .+\(.+\)(?:\s+in .+:\w+)?", RegexOptions.Multiline),
    };

    public LogsController(AppDbContext db, ILogger<LogsController> logger, FeishuNotifier feishu)
    {
        _db = db;
        _logger = logger;
        _feishu = feishu;
    }

    // ===== 1. Skynet 上报日志 =====
    /// <summary>
    /// 接收 Skynet 服务器上报的错误日志
    /// POST /api/logs
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LogIngestResultDto>> Ingest([FromBody] LogIngestDto dto)
    {
        try
        {
            // 提取 traceback
            string? traceback = ExtractTraceback(dto.Msg);

            var entity = new ErrorLog
            {
                Level = dto.Level?.ToLowerInvariant() ?? "error",
                ClusterName = dto.ClusterName ?? "",
                NodeName = dto.NodeName ?? "",
                Tag = dto.Tag ?? "",
                Title = dto.Title ?? "",
                Msg = dto.Msg ?? "",
                Traceback = traceback,
                TimeMs = dto.TimeMs > 0 ? dto.TimeMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CreatedAt = DateTime.UtcNow
            };

            _db.ErrorLogs.Add(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[Ingest] id={Id} level={Level} cluster={Cluster} node={Node}",
                entity.Id, entity.Level, entity.ClusterName, entity.NodeName);

            // 飞书通知（异步，不阻塞返回）
            if (_feishu.ShouldNotify(entity.Level))
            {
                var item = new LogItemDto
                {
                    Id = entity.Id,
                    Level = entity.Level,
                    ClusterName = entity.ClusterName,
                    NodeName = entity.NodeName,
                    Tag = entity.Tag,
                    Title = entity.Title,
                    Msg = entity.Msg,
                    Traceback = entity.Traceback,
                    TimeMs = entity.TimeMs,
                    CreatedAt = entity.CreatedAt
                };
                _ = _feishu.NotifyAsync(item); // fire-and-forget
            }

            return Ok(new LogIngestResultDto { Ok = true, Id = entity.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Ingest] Failed to ingest log");
            return StatusCode(500, new LogIngestResultDto { Ok = false, Error = ex.Message });
        }
    }

    // ===== 2. 查询日志列表 =====
    /// <summary>
    /// 分页查询日志，支持多条件过滤
    /// GET /api/logs
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<LogPagedResultDto>> Query([FromQuery] LogQueryDto query)
    {
        // 空值时设置默认值：start_time 当天 00:00，end_time 当前时间
        var nowTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var todayStartTs = new DateTimeOffset(DateTime.UtcNow.Date).ToUnixTimeMilliseconds();
        var effectiveStartTime = query.start_time > 0 ? query.start_time : todayStartTs;
        var effectiveEndTime = query.end_time > 0 ? query.end_time : nowTs;

        _logger.LogInformation("[Query] start_time={StartTime} end_time={EndTime} level={Level} cluster={Cluster}",
            effectiveStartTime, effectiveEndTime, query.level, query.cluster_name);

        var queryable = _db.ErrorLogs.AsNoTracking();

        // 过滤条件
        if (!string.IsNullOrWhiteSpace(query.level))
            queryable = queryable.Where(l => l.Level == query.level.ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(query.cluster_name))
            queryable = queryable.Where(l => l.ClusterName == query.cluster_name);

        if (!string.IsNullOrWhiteSpace(query.node_name))
            queryable = queryable.Where(l => l.NodeName == query.node_name);

        if (!string.IsNullOrWhiteSpace(query.tag))
            queryable = queryable.Where(l => l.Tag == query.tag);

        if (!string.IsNullOrWhiteSpace(query.keyword))
            queryable = queryable.Where(l => l.Msg.Contains(query.keyword));

        if (effectiveStartTime > 0)
            queryable = queryable.Where(l => l.TimeMs >= effectiveStartTime);

        if (effectiveEndTime > 0)
            queryable = queryable.Where(l => l.TimeMs <= effectiveEndTime);

        // 总数
        var total = await queryable.CountAsync();

        // 分页 + 按时间倒序
        var page = Math.Max(1, query.page);
        var pageSize = Math.Clamp(query.page_size, 1, 100);

        var items = await queryable
            .OrderByDescending(l => l.TimeMs)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LogItemDto
            {
                Id = l.Id,
                Level = l.Level,
                ClusterName = l.ClusterName,
                NodeName = l.NodeName,
                Tag = l.Tag,
                Title = l.Title,
                Msg = l.Msg,
                Traceback = l.Traceback,
                TimeMs = l.TimeMs,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return Ok(new LogPagedResultDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        });
    }

    // ===== 3. 获取过滤选项枚举 =====
    /// <summary>
    /// 返回各过滤字段的可选值（给前端下拉框用）
    /// GET /api/logs/options
    /// </summary>
    [HttpGet("options")]
    public async Task<ActionResult<LogOptionsDto>> GetOptions()
    {
        var logs = _db.ErrorLogs.AsNoTracking();

        var levels = await logs
            .Select(l => l.Level)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

        var clusters = await logs
            .Select(l => l.ClusterName)
            .Where(l => l != "")
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

        var nodes = await logs
            .Select(l => l.NodeName)
            .Where(l => l != "")
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

        var tags = await logs
            .Select(l => l.Tag)
            .Where(l => l != "")
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

        return Ok(new LogOptionsDto
        {
            Levels = levels,
            ClusterNames = clusters,
            NodeNames = nodes,
            Tags = tags
        });
    }

    // ===== 4. 获取单条详情 =====
    /// <summary>
    /// 获取单条日志详情
    /// GET /api/logs/{id}
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<LogItemDto>> GetById(long id)
    {
        var log = await _db.ErrorLogs
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new LogItemDto
            {
                Id = l.Id,
                Level = l.Level,
                ClusterName = l.ClusterName,
                NodeName = l.NodeName,
                Tag = l.Tag,
                Title = l.Title,
                Msg = l.Msg,
                Traceback = l.Traceback,
                TimeMs = l.TimeMs,
                CreatedAt = l.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (log == null)
            return NotFound();

        return Ok(log);
    }

    // ===== Traceback 提取逻辑 =====
    private static string? ExtractTraceback(string? msg)
    {
        if (string.IsNullOrWhiteSpace(msg))
            return null;

        // 优先匹配 Python Traceback
        var pythonMatch = TracebackPatterns[0].Match(msg);
        if (pythonMatch.Success)
            return pythonMatch.Value.Trim();

        // 匹配 Lua stack traceback
        var luaMatch = TracebackPatterns[1].Match(msg);
        if (luaMatch.Success && luaMatch.Length > 10)
            return luaMatch.Value.Trim();

        // 检查是否有 "at " 格式（C# / 通用）
        var atMatches = TracebackPatterns[3].Matches(msg);
        if (atMatches.Count >= 2)
        {
            var lines = msg.Split('\n')
                .Where(l => l.TrimStart().StartsWith("at ") || l.Contains(" in "))
                .Take(20)
                .ToList();
            if (lines.Count >= 2)
                return string.Join("\n", lines);
        }

        return null;
    }
}
