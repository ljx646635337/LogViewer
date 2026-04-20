using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LogViewer.Api.Dtos;

/// <summary>
/// Skynet 服务器上报日志的请求体
/// </summary>
public class LogIngestDto
{
    /// <summary>日志级别: debug/info/warn/error/fatal</summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = "error";

    /// <summary>集群名称</summary>
    [JsonPropertyName("cluster_name")]
    public string ClusterName { get; set; } = "";

    /// <summary>节点名称</summary>
    [JsonPropertyName("node_name")]
    public string NodeName { get; set; } = "";

    /// <summary>标签</summary>
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";

    /// <summary>简短标题</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    /// <summary>消息内容（可能含 traceback）</summary>
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "";

    /// <summary>毫秒时间戳</summary>
    [JsonPropertyName("time_ms")]
    public long TimeMs { get; set; }
}

/// <summary>
/// 单条日志的响应结构
/// </summary>
public class LogItemDto
{
    public long Id { get; set; }
    [JsonPropertyName("level")]
    public string Level { get; set; } = "";
    [JsonPropertyName("cluster_name")]
    public string ClusterName { get; set; } = "";
    [JsonPropertyName("node_name")]
    public string NodeName { get; set; } = "";
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = "";
    [JsonPropertyName("traceback")]
    public string? Traceback { get; set; }
    [JsonPropertyName("time_ms")]
    public long TimeMs { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 日志查询参数（属性名与 URL query string 一致：小写下划线）
/// </summary>
public class LogQueryDto
{
    public string? level { get; set; }
    public string? cluster_name { get; set; }
    public string? node_name { get; set; }
    public string? tag { get; set; }
    public string? keyword { get; set; }
    public long? start_time { get; set; }
    public long? end_time { get; set; }
    public int page { get; set; } = 1;
    public int page_size { get; set; } = 20;
}

/// <summary>
/// 分页查询响应
/// </summary>
public class LogPagedResultDto
{
    public List<LogItemDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>
/// 下拉选项枚举
/// </summary>
public class LogOptionsDto
{
    [JsonPropertyName("levels")]
    public List<string> Levels { get; set; } = new();
    [JsonPropertyName("cluster_names")]
    public List<string> ClusterNames { get; set; } = new();
    [JsonPropertyName("node_names")]
    public List<string> NodeNames { get; set; } = new();
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// 上报结果响应
/// </summary>
public class LogIngestResultDto
{
    public bool Ok { get; set; }
    public long Id { get; set; }
    public string? Error { get; set; }
}
