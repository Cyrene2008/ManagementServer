using System.Text.Json;
using ClassIsland.ManagementServer.Server.Context;
using ClassIsland.ManagementServer.Server.Entities;
using ClassIsland.ManagementServer.Server.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClassIsland.ManagementServer.Server.Services;

public class ObjectsUpdateNotifyService(
    ManagementServerContext context,
    ObjectsAssigneeService objectsAssigneeService,
    WebSocketConnectionManager wsManager,
    ILogger<ObjectsUpdateNotifyService> logger)
{
    private ManagementServerContext DbContext { get; } = context;
    private ObjectsAssigneeService ObjectsAssigneeService { get; } = objectsAssigneeService;
    private WebSocketConnectionManager WsManager { get; } = wsManager;
    private ILogger<ObjectsUpdateNotifyService> Logger { get; } = logger;

    public async Task NotifyObjectUpdatingAsync(Guid id, ObjectTypes objectType)
    {
        var clients = await ObjectsAssigneeService.GetObjectAssignedClients(id);
        foreach (var i in clients)
        {
            await DbContext.ObjectUpdates.AddAsync(new ObjectUpdate()
            {
                ObjectId = id,
                ObjectType = objectType,
                TargetCuid = i.Cuid,
                UpdateTime = DateTime.Now
            });
            await PushDataUpdatedAsync(i.Cuid);
        }
    }
    
    public async Task NotifyClientUpdatingAsync(Guid? cuid=null, string? id=null, long? group=null)
    {
        var clients = await DbContext.Clients.Where(x =>
            (cuid != null && x.Cuid == cuid) ||
            (id != null && x.Id == id) ||
            (group != null && x.AbstractClient.GroupId == group)
        ).Select(x => x).ToListAsync();
        foreach (var i in clients)
        {
            await DbContext.ObjectUpdates.AddAsync(new ObjectUpdate()
            {
                TargetCuid = i.Cuid,
                UpdateTime = DateTime.Now
            });
            await PushDataUpdatedAsync(i.Cuid);
        }
    }

    public void NotifyClientUpdated(string cuid, string id)
    {
        
    }

    public bool IsObjectUpdated(string id)
    {
        return false;
    }

    public bool IsClientUpdated(string cuid, ObjectTypes type)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 通过 WebSocket 推送数据更新通知到客户端
    /// </summary>
    private async Task PushDataUpdatedAsync(Guid cuid)
    {
        try
        {
            var message = JsonSerializer.Serialize(new { type = "DataUpdated" });
            var sent = await WsManager.SendAsync(cuid, message);
            if (sent)
            {
                Logger.LogDebug("已通过 WebSocket 推送 DataUpdated 到 {Cuid}", cuid);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "推送 WebSocket DataUpdated 到 {Cuid} 失败", cuid);
        }
    }
}