# 日志查看器 (LogViewer)

Skynet 服务端错误日志查看系统，支持日志上报与可视化查看。

## 技术栈

| 层 | 技术 |
|----|------|
| 后端 | .NET 8 + ASP.NET Core MVC + EF Core + MySQL |
| 前端 | Vue 3 (CDN) + 原生 HTML/CSS |
| 数据库 | MySQL 8 |

## 快速启动

### 1. 数据库

```sql
CREATE DATABASE log_viewer CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

### 2. 后端 (.NET Core)

```bash
cd backend
dotnet restore
dotnet run --urls="http://0.0.0.0:5001"
# 访问 http://localhost:5001/logViewer
```

修改 `appsettings.json` 中的 `ConnectionStrings:DefaultConnection` 为你的 MySQL 连接串。

### 3. Linux 后台运行

```bash
nohup dotnet backend/LogViewer.dll --urls="http://0.0.0.0:5001" &
```

---

## 页面说明

### 主界面 `/logViewer`
- 今日 Error 数量统计
- 总记录数统计
- 最后上报时间
- 功能面板入口

### 错误日志查看器 `/logViewer/errors`
- 支持按级别（error/warn）、集群、节点、标签筛选
- 时间范围过滤（默认当日 00:00 - 当前时间）
- 关键词模糊搜索
- 堆栈信息展开/收起
- 分页查看
- 测试上报面板

---

## 接口说明

### POST /api/logs
Skynet 服务器上报日志，字段说明：

| 字段 | 类型 | 说明 |
|------|------|------|
| level | string | 日志级别：error / warn |
| cluster_name | string | 集群名称 |
| node_name | string | 节点名称 |
| tag | string | 标签 |
| title | string | 标题（可选） |
| msg | string | 消息内容 |
| time_ms | long | 时间戳（毫秒） |

### GET /api/logs
查询日志列表，支持参数：
- `level` / `cluster_name` / `node_name` / `tag` — 过滤条件
- `keyword` — 模糊搜索 msg
- `start_time` / `end_time` — 时间范围（毫秒时间戳）
- `page` / `page_size` — 分页

### GET /api/logs/options
返回各过滤字段的枚举值。

### GET /api/logs/{id}
获取单条日志详情。

---

## 测试上报

```bash
curl -X POST http://localhost:5001/api/logs \
  -H "Content-Type: application/json" \
  -d '{
    "level": "error",
    "cluster_name": "server1",
    "node_name": "game1",
    "tag": "battle",
    "title": "战斗同步失败",
    "msg": "Battle sync failed.\nTraceback (most recent call last):\n  File \"battle/sync.py\", line 78\n    await channel.send(state)\nBroadcastError: Channel closed",
    "time_ms": '$(date +%s)000'
  }'
```

---

## 目录结构

```
log-viewer/
├── backend/
│   ├── Controllers/
│   │   ├── LogsController.cs          # 日志 API 控制器
│   │   ├── LogViewerController.cs      # 主界面控制器
│   │   └── ErrorsController.cs         # 错误日志查看器控制器
│   ├── Data/AppDbContext.cs            # EF Core 上下文
│   ├── Dtos/LogDtos.cs                # 请求/响应 DTO
│   ├── Models/ErrorLog.cs              # 数据库实体
│   ├── Program.cs                      # 入口
│   ├── Views/
│   │   ├── LogViewer/Index.cshtml     # 主界面视图
│   │   └── Errors/Index.cshtml         # 错误日志查看器视图
│   ├── wwwroot/css/app.css            # 样式文件
│   └── appsettings.json
└── README.md
```

---

## 路由概览

| 路由 | 说明 |
|------|------|
| GET /logViewer | 主界面 Dashboard |
| GET /logViewer/errors | 错误日志查看器 |
| GET /api/logs | 查询日志列表 |
| POST /api/logs | 上报日志 |
