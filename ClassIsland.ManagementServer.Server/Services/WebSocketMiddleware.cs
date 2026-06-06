using System.Net.WebSockets;
using System.Text.Json;

namespace ClassIsland.ManagementServer.Server.Services;

/// <summary>
/// WebSocket 中间件 - 处理 /ws/client 端点
/// </summary>
public class WebSocketMiddleware(
    RequestDelegate next,
    WebSocketConnectionManager connectionManager,
    ILogger<WebSocketMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/ws/client" && context.WebSockets.IsWebSocketRequest)
        {
            var cuidStr = context.Request.Query["cuid"].FirstOrDefault();
            var session = context.Request.Query["session"].FirstOrDefault();

            if (string.IsNullOrEmpty(cuidStr) || !Guid.TryParse(cuidStr, out var cuid))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid cuid");
                return;
            }

            // 验证 session（复用 gRPC 拦截器的逻辑）
            var connectionService = context.RequestServices.GetRequiredService<CyreneMspConnectionService>();
            if (!connectionService.ValidateSession(cuid, session))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Invalid session");
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            connectionManager.Register(cuid, socket);
            logger.LogInformation("客户端 {Cuid} 已通过 WebSocket 连接", cuid);

            await HandleConnection(cuid, socket);
            return;
        }

        await next(context);
    }

    private async Task HandleConnection(Guid cuid, WebSocket socket)
    {
        var buffer = new byte[4096];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessage(cuid, message, socket);
                }
            }
        }
        catch (WebSocketException)
        {
            // 连接异常断开
        }
        finally
        {
            connectionManager.Unregister(cuid);
            logger.LogInformation("客户端 {Cuid} WebSocket 断开", cuid);
            if (socket.State != WebSocketState.Closed)
            {
                try { socket.Dispose(); } catch { }
            }
        }
    }

    private async Task HandleMessage(Guid cuid, string message, WebSocket socket)
    {
        try
        {
            var doc = JsonDocument.Parse(message);
            var type = doc.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "Pong":
                    // 心跳回复，忽略
                    break;
                default:
                    logger.LogDebug("收到客户端 {Cuid} 的未知消息类型: {Type}", cuid, type);
                    break;
            }
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "处理客户端 {Cuid} 消息时出错", cuid);
        }
    }
}
