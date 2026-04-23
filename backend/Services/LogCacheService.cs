using System.Collections.Concurrent;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LogViewer.Api.Data;
using LogViewer.Api.Dtos;

namespace LogViewer.Api.Services;

/// <summary>
/// 日志缓存服务
/// </summary>
public class LogCacheService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<LogCacheService> _logger;
    private readonly int _maxLogs;
    private readonly int _cacheDays;
    private readonly int _refreshCooldownSeconds;

    // 缓存引用，无锁读取
    private volatile LogCacheData? _cache;

    // 写入锁：追加日志、刷新缓存时加锁
    private readonly object _writeLock = new();

    // 最后刷新时间戳（毫秒，用于冷却限制），用 Interlocked 保证原子操作
    private long _lastRefreshTimeMs = 0;

    public LogCacheService(IServiceProvider sp, ILogger<LogCacheService> logger, IConfiguration config)
    {
        _sp = sp;
        _logger = logger;
        _maxLogs = config.GetValue("Cache:MaxLogs", 10000);
        _cacheDays = config.GetValue("Cache:CacheDays", 7);
        _refreshCooldownSeconds = config.GetValue("Cache:RefreshCooldownSeconds", 3);
    }

    /// <summary>
    /// 启动时异步加载缓存
    /// </summary>
    public async Task WarmUpAsync()
    {
        await RebuildCacheAsync();
    }

    /// <summary>
    /// 查询日志（7天内走缓存，超出7天查DB）
    /// </summary>
    public async Task<LogPagedResultDto> QueryAsync(LogQueryDto query)
    {
        var now = DateTimeOffset.UtcNow.AddHours(8); // 东八区当前时间
        var sevenDaysAgo = now.AddDays(-_cacheDays).ToUnixTimeMilliseconds();

        var startTs = query.start_time ?? 0;
        var endTs = query.end_time ?? now.ToUnixTimeMilliseconds();

        // 查询范围超出7天，直接查DB
        if (endTs < sevenDaysAgo)
        {
            _logger.LogDebug("[Cache] 查询超出7天，直接查DB startTs={StartTs} endTs={EndTs}", startTs, endTs);
            return await QueryFromDbAsync(query);
        }

        // 缓存为空，触发重建
        var cache = _cache;
        if (cache == null)
        {
            _logger.LogDebug("[Cache] 缓存为空，触发重建");
            await RebuildCacheAsync();
            cache = _cache;
        }

        if (cache == null)
        {
            return await QueryFromDbAsync(query);
        }

        // 从缓存查询
        return QueryFromCache(cache, query);
    }

    /// <summary>
    /// 追加新日志到缓存
    /// </summary>
    public void AppendLog(LogItemDto log)
    {
        lock (_writeLock)
        {
            if (_cache == null) return;

            _cache.AllLogs.Insert(0, log);

            // 清理过期数据
            CleanupExpiredData();

            // 更新 stats
            UpdateStats(log);

            _logger.LogDebug("[Cache] 追加日志 id={Id}", log.Id);
        }
    }

    /// <summary>
    /// 获取剩余冷却时间（秒）
    /// </summary>
    public double GetCooldownRemainingSeconds()
    {
        var now = DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeMilliseconds();
        var lastMs = Interlocked.Read(ref _lastRefreshTimeMs);
        var elapsed = (now - lastMs) / 1000.0;
        return Math.Max(0, _refreshCooldownSeconds - elapsed);
    }

    /// <summary>
    /// 刷新缓存（强制重建，有冷却限制）
    /// </summary>
    public async Task<(bool success, double cooldownRemaining)> RefreshAsync()
    {
        var nowMs = DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeMilliseconds();
        var lastMs = Interlocked.Read(ref _lastRefreshTimeMs);

        // 冷却检查
        var elapsed = (nowMs - lastMs) / 1000.0;
        if (elapsed < _refreshCooldownSeconds)
        {
            var remaining = _refreshCooldownSeconds - elapsed;
            _logger.LogWarning("[Cache] 刷新冷却中，还需等待 {Remaining:F1} 秒", remaining);
            return (false, remaining);
        }

        Interlocked.Exchange(ref _lastRefreshTimeMs, nowMs);
        await RebuildCacheAsync();
        return (true, 0);
    }

    /// <summary>
    /// 获取 Dashboard 统计数据
    /// </summary>
    public async Task<(int todayErrorCount, int totalCount, long lastLogTime)> GetStatsAsync()
    {
        var cache = _cache;

        // 缓存命中
        if (cache != null)
        {
            var now = DateTimeOffset.UtcNow.AddHours(8);
            var todayStart = new DateTimeOffset(now.Date).ToUnixTimeMilliseconds();
            var todayErrorCount = cache.AllLogs.Count(l => l.Level == "error" && l.TimeMs >= todayStart);
            var totalCount = cache.AllLogs.Count;
            var lastLogTime = cache.AllLogs.FirstOrDefault()?.TimeMs ?? 0;

            return (todayErrorCount, totalCount, lastLogTime);
        }

        // 缓存未命中，查 DB
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now2 = DateTimeOffset.UtcNow.AddHours(8);
        var todayStart2 = new DateTimeOffset(now2.Date).ToUnixTimeMilliseconds();
        var sevenDaysAgo = now2.AddDays(-_cacheDays).ToUnixTimeMilliseconds();

        var todayErrorCount2 = await db.ErrorLogs
            .Where(l => l.Level == "error" && l.TimeMs >= todayStart2)
            .CountAsync();

        var totalCount2 = await db.ErrorLogs
            .Where(l => l.TimeMs >= sevenDaysAgo)
            .CountAsync();

        var lastLogTime2 = await db.ErrorLogs
            .OrderByDescending(l => l.TimeMs)
            .Select(l => (long?)l.TimeMs)
            .FirstOrDefaultAsync();

        return (todayErrorCount2, totalCount2, lastLogTime2 ?? 0);
    }

    #region Private Methods

    /// <summary>
    /// 重建缓存（加锁，原子替换引用）
    /// </summary>
    private async Task RebuildCacheAsync()
    {
        List<LogItemDto> logs;
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTimeOffset.UtcNow.AddHours(8);
            var sevenDaysAgo = now.AddDays(-_cacheDays).ToUnixTimeMilliseconds();

            logs = await db.ErrorLogs
                .AsNoTracking()
                .Where(l => l.TimeMs >= sevenDaysAgo)
                .OrderByDescending(l => l.TimeMs)
                .Take(_maxLogs)
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
        }

        lock (_writeLock)
        {
            _cache = new LogCacheData { AllLogs = logs };
            _logger.LogInformation("[Cache] 缓存重建完成，共 {Count} 条", logs.Count);
        }
    }

    /// <summary>
    /// 从缓存查询
    /// </summary>
    private LogPagedResultDto QueryFromCache(LogCacheData cache, LogQueryDto query)
    {
        var logs = cache.AllLogs.AsEnumerable();

        // 过滤条件
        if (!string.IsNullOrWhiteSpace(query.level))
            logs = logs.Where(l => l.Level == query.level);

        if (!string.IsNullOrWhiteSpace(query.cluster_name))
            logs = logs.Where(l => l.ClusterName == query.cluster_name);

        if (!string.IsNullOrWhiteSpace(query.node_name))
            logs = logs.Where(l => l.NodeName == query.node_name);

        if (!string.IsNullOrWhiteSpace(query.tag))
            logs = logs.Where(l => l.Tag == query.tag);

        if (!string.IsNullOrWhiteSpace(query.keyword))
            logs = logs.Where(l => l.Msg.Contains(query.keyword));

        if (query.start_time > 0)
            logs = logs.Where(l => l.TimeMs >= query.start_time);

        if (query.end_time > 0)
            logs = logs.Where(l => l.TimeMs <= query.end_time);

        var total = logs.Count();

        var page = Math.Max(1, query.page);
        var pageSize = Math.Clamp(query.page_size, 1, 100);
        var items = logs
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new LogPagedResultDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 直接从 DB 查询
    /// </summary>
    private async Task<LogPagedResultDto> QueryFromDbAsync(LogQueryDto query)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var queryable = db.ErrorLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.level))
            queryable = queryable.Where(l => l.Level == query.level);

        if (!string.IsNullOrWhiteSpace(query.cluster_name))
            queryable = queryable.Where(l => l.ClusterName == query.cluster_name);

        if (!string.IsNullOrWhiteSpace(query.node_name))
            queryable = queryable.Where(l => l.NodeName == query.node_name);

        if (!string.IsNullOrWhiteSpace(query.tag))
            queryable = queryable.Where(l => l.Tag == query.tag);

        if (!string.IsNullOrWhiteSpace(query.keyword))
            queryable = queryable.Where(l => l.Msg.Contains(query.keyword));

        if (query.start_time > 0)
            queryable = queryable.Where(l => l.TimeMs >= query.start_time);

        if (query.end_time > 0)
            queryable = queryable.Where(l => l.TimeMs <= query.end_time);

        var total = await queryable.CountAsync();

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

        return new LogPagedResultDto
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <summary>
    /// 清理过期数据
    /// </summary>
    private void CleanupExpiredData()
    {
        if (_cache == null) return;

        var now = DateTimeOffset.UtcNow.AddHours(8);
        var cutoff = now.AddDays(-_cacheDays).ToUnixTimeMilliseconds();

        // 移除7天前的数据
        _cache.AllLogs.RemoveAll(l => l.TimeMs < cutoff);

        // 如果还超过上限，按时间倒序保留最新的
        if (_cache.AllLogs.Count > _maxLogs)
        {
            _cache.AllLogs = _cache.AllLogs
                .OrderByDescending(l => l.TimeMs)
                .Take(_maxLogs)
                .ToList();
        }
    }

    /// <summary>
    /// 更新 stats
    /// </summary>
    private void UpdateStats(LogItemDto log)
    {
        // stats 通过 AllLogs 实时计算，不需要额外更新
    }

    #endregion
}

/// <summary>
/// 缓存数据结构（内部使用）
/// </summary>
internal class LogCacheData
{
    public List<LogItemDto> AllLogs { get; set; } = new();
}
