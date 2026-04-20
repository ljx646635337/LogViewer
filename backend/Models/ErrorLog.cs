using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogViewer.Api.Models;

/// <summary>
/// 错误日志实体
/// </summary>
[Table("error_logs")]
public class ErrorLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>日志级别: debug/info/warn/error/fatal</summary>
    [Column("level")]
    [MaxLength(20)]
    public string Level { get; set; } = "error";

    /// <summary>集群名称</summary>
    [Column("cluster_name")]
    [MaxLength(64)]
    public string ClusterName { get; set; } = "";

    /// <summary>节点名称</summary>
    [Column("node_name")]
    [MaxLength(64)]
    public string NodeName { get; set; } = "";

    /// <summary>标签，如 auth/db/battle</summary>
    [Column("tag")]
    [MaxLength(128)]
    public string Tag { get; set; } = "";

    /// <summary>简短标题</summary>
    [Column("title")]
    [MaxLength(255)]
    public string Title { get; set; } = "";

    /// <summary>原始消息（含 traceback 的完整文本）</summary>
    [Column("msg")]
    public string Msg { get; set; } = "";

    /// <summary>从 msg 中提取的堆栈信息（独立存储，方便查询）</summary>
    [Column("traceback")]
    public string? Traceback { get; set; }

    /// <summary>Skynet 上报的毫秒时间戳</summary>
    [Column("time_ms")]
    public long TimeMs { get; set; }

    /// <summary>记录入库时间</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
