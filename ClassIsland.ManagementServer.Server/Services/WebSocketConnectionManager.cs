using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace ClassIsland.ManagementServer.Server.Services;

/// <summary>
/// WebSocket 连接管理器
/// </summary>
public class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<Guid, WebSocket> _connections = new();

    /// <summary>
    /// 注册客户端 WebSocket 连接
    /// </summary>
    public void Register(Guid cuid, WebSocket socket)
    {
        _connections[cuid] = socket;
    }

    /// <summary>
    /// 移除客户端连接
    /// </summary>
    public void Unregister(Guid cuid)
    {
        _connections.TryRemove(cuid, out _);
    }

    /// <summary>
    /// 获取客户端连接
    /// </summary>
    public WebSocket? GetConnection(Guid cuid)
    {
        _connections.TryGetValue(cuid, out var socket);
        return socket;
    }

    /// <summary>
    /// 检查客户端是否在线
    /// </summary>
    public bool IsOnline(Guid cuid)
    {
        var socket = GetConnection(cuid);
        return socket is { State: WebSocketState.Open };
    }

    /// <summary>
    /// 获取所有在线客户端 CUID
    /// </summary>
    public IEnumerable<Guid> GetOnlineClients()
    {
        return _connections
            .Where(kv => kv.Value.State == WebSocketState.Open)
            .Select(kv => kv.Key);
    }

    /// <summary>
    /// 在线客户端数量
    /// </summary>
    public int OnlineCount => _connections.Count(kv => kv.Value.State == WebSocketState.Open);

    /// <summary>
    /// 向指定客户端发送消息
    /// </summary>
    public async Task<bool> SendAsync(Guid cuid, string message, CancellationToken ct = default)
    {
        var socket = GetConnection(cuid);
        if (socket is not { State: WebSocketState.Open })
            return false;

        try
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                ct);
            return true;
        }
        catch
        {
            Unregister(cuid);
            return false;
        }
    }
}
