using System.Text.Json;
using ClassIsland.ManagementServer.Server.Authorization;
using ClassIsland.ManagementServer.Server.Context;
using ClassIsland.ManagementServer.Server.Entities;
using ClassIsland.ManagementServer.Server.Extensions;
using ClassIsland.ManagementServer.Server.Models;
using ClassIsland.ManagementServer.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassIsland.ManagementServer.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/commands")]
public class CommandsController(
    ILogger<CommandsController> logger,
    ManagementServerContext dbContext,
    WebSocketConnectionManager wsManager,
    CyreneMspConnectionService connectionService) : ControllerBase
{
    public ILogger<CommandsController> Logger { get; } = logger;
    public ManagementServerContext DbContext { get; } = dbContext;
    public WebSocketConnectionManager WsManager { get; } = wsManager;
    public CyreneMspConnectionService ConnectionService { get; } = connectionService;

    /// <summary>
    /// 发送命令到客户端
    /// </summary>
    [HttpPost("execute")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.CommandsUser}")]
    public async Task<IActionResult> ExecuteCommand([FromBody] ExecuteCommandRequest request)
    {
        var client = await DbContext.Clients.FindAsync(request.ClientCuid);
        if (client == null)
            return NotFound(new Error("客户端不存在"));

        var command = new RemoteCommand
        {
            ClientCuid = request.ClientCuid,
            Command = request.Command,
            Shell = request.Shell,
            TimeoutSeconds = request.TimeoutSeconds > 0 ? request.TimeoutSeconds : 30
        };

        await DbContext.RemoteCommands.AddAsync(command);
        await DbContext.SaveChangesAsync();

        // 尝试通过 WebSocket 推送
        var wsMessage = JsonSerializer.Serialize(new
        {
            type = "ExecuteCommand",
            payload = new
            {
                commandId = command.Id,
                command = command.Command,
                shell = command.Shell,
                timeoutSeconds = command.TimeoutSeconds
            }
        });

        var sent = await WsManager.SendAsync(request.ClientCuid, wsMessage);

        if (!sent)
        {
            // 降级到 gRPC（客户端通过 ListenCommand 流接收）
            Logger.LogInformation("WebSocket 推送失败，命令 {Id} 将通过 gRPC 或心跳拉取", command.Id);
        }

        Logger.LogInformation("已发送命令 {Id} 到客户端 {Cuid}", command.Id, request.ClientCuid);
        return Ok(command);
    }

    /// <summary>
    /// 查询命令状态
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetCommand(long id)
    {
        var command = await DbContext.RemoteCommands.FindAsync(id);
        if (command == null)
            return NotFound(new Error("命令不存在"));
        return Ok(command);
    }

    /// <summary>
    /// 列出命令历史
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListCommands([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? clientCuid = null, [FromQuery] int? status = null)
    {
        var query = DbContext.RemoteCommands.AsQueryable();
        if (clientCuid.HasValue)
            query = query.Where(x => x.ClientCuid == clientCuid.Value);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var result = await query.ToPaginatedListAsync(pageIndex, pageSize, decreasing: true, orderByUpdatedTime: true);
        return Ok(result);
    }

    /// <summary>
    /// 客户端回传命令结果（匿名）
    /// </summary>
    [HttpPost("{id:long}/result")]
    [AllowAnonymous]
    public async Task<IActionResult> SubmitResult(long id, [FromBody] CommandResultRequest request)
    {
        var command = await DbContext.RemoteCommands.FindAsync(id);
        if (command == null)
            return NotFound(new Error("命令不存在"));

        command.ExitCode = request.ExitCode;
        command.Stdout = request.Stdout;
        command.Stderr = request.Stderr;
        command.Status = request.ExitCode == 0 ? 2 : 3; // Completed or Failed
        command.CompletedTime = DateTime.Now;
        command.UpdatedTime = DateTime.Now;

        await DbContext.SaveChangesAsync();
        Logger.LogInformation("命令 {Id} 执行完成，退出码: {ExitCode}", id, request.ExitCode);
        return Ok();
    }
}

public class ExecuteCommandRequest
{
    public Guid ClientCuid { get; set; }
    public string Command { get; set; } = "";
    public int Shell { get; set; } = 0;
    public int TimeoutSeconds { get; set; } = 30;
}

public class CommandResultRequest
{
    public int ExitCode { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
}
