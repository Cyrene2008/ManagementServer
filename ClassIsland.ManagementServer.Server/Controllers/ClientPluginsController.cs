using ClassIsland.ManagementServer.Server.Authorization;
using ClassIsland.ManagementServer.Server.Context;
using ClassIsland.ManagementServer.Server.Entities;
using ClassIsland.ManagementServer.Server.Enums;
using ClassIsland.ManagementServer.Server.Services;
using ClassIsland.Shared.Protobuf;
using ClassIsland.Shared.Protobuf.Command;
using ClassIsland.Shared.Protobuf.Enum;
using Google.Protobuf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassIsland.ManagementServer.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/clients/{cuid}/plugins")]
public class ClientPluginsController(
    ILogger<ClientPluginsController> logger,
    ManagementServerContext dbContext,
    ClientCommandDeliverService commandDeliverService,
    PendingConfigRequestService pendingConfigRequests) : ControllerBase
{
    public ILogger<ClientPluginsController> Logger { get; } = logger;
    public ManagementServerContext DbContext { get; } = dbContext;
    public ClientCommandDeliverService CommandDeliverService { get; } = commandDeliverService;
    public PendingConfigRequestService PendingConfigRequests { get; } = pendingConfigRequests;

    /// <summary>
    /// 获取客户端已安装的插件列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPlugins(Guid cuid)
    {
        var plugins = await DbContext.ClientPluginInfos
            .Where(p => p.ClientCuid == cuid)
            .OrderBy(p => p.PluginName)
            .ToListAsync();
        return Ok(plugins);
    }

    /// <summary>
    /// 请求客户端上报插件列表
    /// </summary>
    [HttpPost("request-upload")]
    [Authorize(Roles = Roles.ObjectsWrite)]
    public async Task<IActionResult> RequestUpload(Guid cuid)
    {
        var payload = new GetClientConfig
        {
            RequestGuid = Guid.NewGuid().ToString(),
            ConfigType = ConfigTypes.PluginList
        };

        PendingConfigRequests.TrackRequest(payload.RequestGuid, ConfigTypes.PluginList, cuid);

        await CommandDeliverService.DeliverCommandAsync(
            CommandTypes.GetClientConfig,
            payload,
            new ObjectsAssignee
            {
                AssigneeType = AssigneeTypes.ClientUid,
                TargetClientCuid = cuid
            });

        Logger.LogInformation("已请求客户端 {Cuid} 上报插件列表", cuid);
        return Ok(new { success = true, requestId = payload.RequestGuid });
    }

    /// <summary>
    /// 远程安装插件（上传 .cipx 文件）
    /// </summary>
    [HttpPost("install")]
    [Authorize(Roles = Roles.ObjectsWrite)]
    public async Task<IActionResult> InstallPlugin(Guid cuid, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "请上传 .cipx 文件" });

        if (!file.FileName.EndsWith(".cipx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "文件必须是 .cipx 格式" });

        // 保存到服务器临时目录
        var uploadsDir = Path.Combine("data", "plugin-uploads");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{cuid}_{DateTime.Now:yyyyMMddHHmmss}_{file.FileName}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        // 读取文件内容为 base64
        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        var base64Content = Convert.ToBase64String(fileBytes);

        // 通过远程命令让客户端下载并安装
        // 使用 ExecuteCommand 在客户端执行下载操作
        // 注意：这里使用 WebSocket 推送文件路径，客户端需要能访问服务器文件
        // 更好的方案是通过 gRPC 直接传输文件

        // 存储安装请求，等待客户端拉取
        var installRequest = new ClientPluginInstallRequest
        {
            ClientCuid = cuid,
            FileName = fileName,
            FilePath = filePath,
            Status = 0, // Pending
            CreatedTime = DateTime.Now,
            UpdatedTime = DateTime.Now
        };
        await DbContext.ClientPluginInstallRequests.AddAsync(installRequest);
        await DbContext.SaveChangesAsync();

        // 推送安装命令到客户端
        var command = new RemoteExecuteCommand
        {
            CommandId = installRequest.Id,
            Command = $"install-plugin:{installRequest.Id}",
            Shell = 0,
            TimeoutSeconds = 60
        };
        await CommandDeliverService.DeliverCommandAsync(
            CommandTypes.ExecuteCommand,
            command,
            new ObjectsAssignee
            {
                AssigneeType = AssigneeTypes.ClientUid,
                TargetClientCuid = cuid
            });

        Logger.LogInformation("已发送插件安装命令到客户端 {Cuid}, 文件: {FileName}", cuid, fileName);
        return Ok(new { success = true, requestId = installRequest.Id });
    }

    /// <summary>
    /// 获取插件安装包（供客户端拉取）
    /// </summary>
    [HttpGet("install/{requestId}/package")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPluginPackage(Guid cuid, long requestId)
    {
        var request = await DbContext.ClientPluginInstallRequests.FindAsync(requestId);
        if (request == null || request.ClientCuid != cuid)
            return NotFound();

        if (!System.IO.File.Exists(request.FilePath))
            return NotFound(new { error = "安装包不存在" });

        var fileBytes = await System.IO.File.ReadAllBytesAsync(request.FilePath);
        request.Status = 1; // Downloaded
        request.UpdatedTime = DateTime.Now;
        await DbContext.SaveChangesAsync();

        return File(fileBytes, "application/octet-stream", "plugin.cipx");
    }

    /// <summary>
    /// 卸载插件
    /// </summary>
    [HttpPost("{pluginId}/uninstall")]
    [Authorize(Roles = Roles.ObjectsWrite)]
    public async Task<IActionResult> UninstallPlugin(Guid cuid, string pluginId)
    {
        var command = new RemoteExecuteCommand
        {
            CommandId = 0,
            Command = $"uninstall-plugin:{pluginId}",
            Shell = 0,
            TimeoutSeconds = 30
        };
        await CommandDeliverService.DeliverCommandAsync(
            CommandTypes.ExecuteCommand,
            command,
            new ObjectsAssignee
            {
                AssigneeType = AssigneeTypes.ClientUid,
                TargetClientCuid = cuid
            });

        Logger.LogInformation("已发送插件卸载命令到客户端 {Cuid}, 插件: {PluginId}", cuid, pluginId);
        return Ok(new { success = true });
    }

    /// <summary>
    /// 启用/禁用插件
    /// </summary>
    [HttpPost("{pluginId}/toggle")]
    [Authorize(Roles = Roles.ObjectsWrite)]
    public async Task<IActionResult> TogglePlugin(Guid cuid, string pluginId, [FromQuery] bool enable)
    {
        var action = enable ? "enable-plugin" : "disable-plugin";
        var command = new RemoteExecuteCommand
        {
            CommandId = 0,
            Command = $"{action}:{pluginId}",
            Shell = 0,
            TimeoutSeconds = 30
        };
        await CommandDeliverService.DeliverCommandAsync(
            CommandTypes.ExecuteCommand,
            command,
            new ObjectsAssignee
            {
                AssigneeType = AssigneeTypes.ClientUid,
                TargetClientCuid = cuid
            });

        Logger.LogInformation("已发送插件{Action}命令到客户端 {Cuid}, 插件: {PluginId}", enable ? "启用" : "禁用", cuid, pluginId);
        return Ok(new { success = true });
    }
}
