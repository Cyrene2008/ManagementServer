# 集控增强设计文档：远程命令执行 + WebSocket 推送 + 首页看板

**日期:** 2026-06-06
**范围:** ManagementServer 服务端 + ClassIsland-Cyrene-MSP 客户端 + WebUI

---

## 1. 远程命令执行

### 1.1 数据模型

**RemoteCommand 实体:**

| 字段 | 类型 | 说明 |
|------|------|------|
| Id | long (PK) | 命令 ID |
| ClientCuid | Guid | 目标客户端 |
| Command | string | 要执行的命令（cmd /c 或 pwsh -c） |
| Shell | enum | Cmd=0, PowerShell=1 |
| Status | enum | Pending=0, Running=1, Completed=2, Failed=3, Timeout=4 |
| ExitCode | int? | 执行结果退出码 |
| Stdout | string | 标准输出 |
| Stderr | string | 标准错误 |
| TimeoutSeconds | int | 超时时间（默认 30 秒） |
| CreatedTime | DateTime | 创建时间 |
| UpdatedTime | DateTime | 更新时间 |
| CompletedTime | DateTime? | 完成时间 |

### 1.2 授权

在 `ManagementCredentialConfig` 新增字段：

```csharp
public AuthorizeLevel ExecuteCommandAuthorizeLevel { get; set; } = AuthorizeLevel.Admin;
```

默认 Admin 级别，可通过策略配置为 User 或 None。

### 1.3 API 端点

| 方法 | 路径 | 说明 | 授权 |
|------|------|------|------|
| POST | /api/v1/commands/execute | 发送命令到客户端 | ExecuteCommand 权限 |
| GET | /api/v1/commands/{id} | 查询命令状态和结果 | 已认证 |
| GET | /api/v1/commands | 列出命令历史（分页） | 已认证 |
| POST | /api/v1/commands/{id}/result | 客户端回传结果（匿名） | 无（客户端调用） |

**POST /api/v1/commands/execute 请求体:**
```json
{
  "clientCuid": "uuid",
  "command": "dir C:\\",
  "shell": 0,
  "timeoutSeconds": 30
}
```

**POST /api/v1/commands/{id}/result 请求体:**
```json
{
  "exitCode": 0,
  "stdout": "...",
  "stderr": ""
}
```

### 1.4 命令下发流程

```
管理员 → POST /api/v1/commands/execute
    → 服务端创建 RemoteCommand (Status=Pending)
    → 优先 WebSocket 推送 ExecuteCommand 到客户端
    → 若 WS 失败 → 降级 gRPC ListenCommand 流
    → 若两者都失败 → 命令保持 Pending，客户端下次心跳拉取

客户端收到命令
    → 启动进程执行（cmd /c 或 pwsh -c）
    → 捕获 stdout/stderr
    → POST /api/v1/commands/{id}/result
    → 服务端更新 Status + 输出
```

### 1.5 客户端处理

客户端需要新增：
- WebSocket 消息处理：识别 `ExecuteCommand` 类型
- 命令执行器：使用 `System.Diagnostics.Process` 启动进程
- 超时控制：超时后 Kill 进程并返回 Timeout 状态
- 结果回传：通过 REST API POST 回服务端

---

## 2. WebSocket 实时推送

### 2.1 服务端

**端点:** `ws://host/ws/client`

**协议:**
- 客户端连接时通过 query string 传递 `cuid` 和 `session`
- 服务端验证 session 有效性后注册到连接池
- 服务端可推送 JSON 消息到客户端

**消息格式:**
```json
{
  "type": "ExecuteCommand|PolicyUpdated|DataUpdated|Ping",
  "payload": { ... }
}
```

**心跳:** 服务端每 30 秒发送 Ping，客户端回复 Pong，超时 60 秒断开。

### 2.2 客户端

- 启动后建立 WebSocket 连接
- 监听消息并分发处理
- 断开后自动重连（指数退避，最大 30 秒）
- gRPC ListenCommand 流保留作为备用通道

### 2.3 连接管理

服务端维护 `ConcurrentDictionary<Guid, WebSocket>` 连接池：
- 客户端连接时注册
- 断开时移除
- 推送时先查 WS 连接，不存在则降级到 gRPC

---

## 3. 首页看板

### 3.1 API

**GET /api/v1/dashboard/overview**

返回:
```json
{
  "onlineCount": 5,
  "totalCount": 20,
  "groupDistribution": [
    { "groupName": "默认", "count": 15 },
    { "groupName": "三年一班", "count": 5 }
  ],
  "auditEventTrend": [
    { "date": "2026-06-01", "count": 12 },
    { "date": "2026-06-02", "count": 8 }
  ],
  "recentCommands": [
    { "id": 1, "clientCuid": "...", "command": "dir", "status": 2, "createdTime": "..." }
  ],
  "versionDistribution": [
    { "version": "2.0.4.0", "count": 18 }
  ],
  "policyCoverage": {
    "totalClients": 20,
    "coveredClients": 15,
    "coverageRate": 0.75
  }
}
```

### 3.2 WebUI

使用 Naive UI 组件：
- **统计卡片行:** 在线数、总数、在线率、今日事件数
- **分组分布饼图:** ECharts 饼图
- **审计趋势折线图:** ECharts 折线图（最近 7 天）
- **命令历史表格:** NDataTable，显示最近 10 条
- **版本分布:** 饼图或条形图
- **策略覆盖:** 进度条或环形图

---

## 4. 数据库变更

新增 `RemoteCommands` 表 + `ExecuteCommandAuthorizeLevel` 字段到 `ManagementCredentialConfig`。

需要 EF Core 迁移。

---

## 5. 依赖关系

```
WebSocket 推送（独立）
    ↓
远程命令执行（依赖 WebSocket 作为主通道）
    ↓
首页看板（独立，只读聚合查询）
```

建议实现顺序：WebSocket → 命令执行 → 看板
