using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using LogViewer.Api.Dtos;

namespace LogViewer.Api.Services;

/// <summary>
/// 飞书机器人通知服务
/// </summary>
public class FeishuNotifier
{
    private readonly IConfiguration _config;
    private readonly ILogger<FeishuNotifier> _logger;

    public FeishuNotifier(IConfiguration config, ILogger<FeishuNotifier> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 检查是否需要通知（根据级别和配置）
    /// </summary>
    public bool ShouldNotify(string level)
    {
        if (!_config.GetValue<bool>("Feishu:Enabled"))
            return false;

        var levelUpper = level?.ToUpperInvariant() ?? "";
        if (levelUpper == "ERROR" && _config.GetValue<bool>("Feishu:NotifyOnError"))
            return true;
        if (levelUpper == "TRACE" && _config.GetValue<bool>("Feishu:NotifyOnTrace"))
            return true;

        return false;
    }

    /// <summary>
    /// 发送飞书通知
    /// </summary>
    public async Task NotifyAsync(LogItemDto log)
    {
        var webhook = _config["Feishu:Webhook"];
        if (string.IsNullOrEmpty(webhook) || webhook.Contains("xxxxxxxx"))
        {
            _logger.LogWarning("[Feishu] Webhook 未配置，跳过通知");
            return;
        }

        var body = BuildMessage(log);
        var json = JsonSerializer.Serialize(body);

        try
        {
            using var client = new HttpClient();
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(webhook, content);
            var respBody = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Feishu] 通知发送成功 id={Id} level={Level}", log.Id, log.Level);
            }
            else
            {
                _logger.LogWarning("[Feishu] 通知发送失败 status={Status} body={Body}", resp.StatusCode, respBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Feishu] 通知发送异常");
        }
    }

    /// <summary>
    /// 构建飞书卡片消息
    /// </summary>
    private object BuildMessage(LogItemDto log)
    {
        var time = log.TimeMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(log.TimeMs).ToString("yyyy-MM-dd HH:mm:ss")
            : log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        var msgPreview = log.Msg?.Length > 200
            ? log.Msg[..200] + "..."
            : log.Msg ?? "";

        var levelEmoji = log.Level?.ToUpperInvariant() switch
        {
            "ERROR" => "🔴",
            "TRACE" => "🟠",
            "WARN" => "⚠️",
            "INFO" => "ℹ️",
            _ => "📋"
        };

        return new
        {
            msg_type = "interactive",
            card = new
            {
                header = new
                {
                    title = new { tag = "plain_text", content = $"{levelEmoji} {log.Level?.ToUpperInvariant() ?? "LOG"} 日志告警" },
                    template = log.Level?.ToUpperInvariant() == "ERROR" ? "red" : "orange"
                },
                elements = new object[]
                {
                    new { tag = "div", text = new { tag = "lark_md", content = $"**集群:** {Escape(log.ClusterName)}\n**节点:** {Escape(log.NodeName)}\n**标签:** {Escape(log.Tag ?? "-")}\n**时间:** {time}" } },
                    new { tag = "hr" },
                    new { tag = "div", text = new { tag = "lark_md", content = $"**标题:**\n{Escape(log.Title ?? "-")}" } },
                    new { tag = "div", text = new { tag = "lark_md", content = $"**消息:**\n```\n{Escape(msgPreview)}\n```" } },
                    new { tag = "note", elements = new[] { new { tag = "plain_text", content = $"日志ID: {log.Id}" } } }
                }
            }
        };
    }

    private static string Escape(string? s) =>
        string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
}
