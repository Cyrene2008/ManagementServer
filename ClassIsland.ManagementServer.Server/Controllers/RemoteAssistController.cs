using ClassIsland.ManagementServer.Server.Context;
using ClassIsland.ManagementServer.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassIsland.ManagementServer.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/clients/{cuid}/remote-assist")]
public class RemoteAssistController(
    ILogger<RemoteAssistController> logger,
    ManagementServerContext dbContext) : ControllerBase
{
    public ILogger<RemoteAssistController> Logger { get; } = logger;
    public ManagementServerContext DbContext { get; } = dbContext;

    /// <summary>
    /// 获取远程协助状态（客户端调用）
    /// </summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus(Guid cuid)
    {
        var client = await DbContext.Clients.FindAsync(cuid);
        if (client == null)
            return NotFound(new Error("客户端不存在"));

        return Ok(new
        {
            enabled = client.RemoteAssistEnabled,
            pin = client.RemoteAssistPin ?? ""
        });
    }

    /// <summary>
    /// 启用远程协助（客户端调用，设置 PIN）
    /// </summary>
    [HttpPost("enable")]
    [AllowAnonymous]
    public async Task<IActionResult> Enable(Guid cuid, [FromBody] EnableRemoteAssistRequest request)
    {
        var client = await DbContext.Clients.FindAsync(cuid);
        if (client == null)
            return NotFound(new Error("客户端不存在"));

        client.RemoteAssistEnabled = true;
        client.RemoteAssistPin = request.Pin;
        client.UpdatedTime = DateTime.Now;
        await DbContext.SaveChangesAsync();

        Logger.LogInformation("客户端 {Cuid} 启用远程协助，PIN: {Pin}", cuid, request.Pin);
        return Ok(new { success = true });
    }

    /// <summary>
    /// 禁用远程协助（客户端调用）
    /// </summary>
    [HttpPost("disable")]
    [AllowAnonymous]
    public async Task<IActionResult> Disable(Guid cuid)
    {
        var client = await DbContext.Clients.FindAsync(cuid);
        if (client == null)
            return NotFound(new Error("客户端不存在"));

        client.RemoteAssistEnabled = false;
        client.RemoteAssistPin = null;
        client.UpdatedTime = DateTime.Now;
        await DbContext.SaveChangesAsync();

        Logger.LogInformation("客户端 {Cuid} 禁用远程协助", cuid);
        return Ok(new { success = true });
    }

    /// <summary>
    /// 验证 PIN 并更新（管理端调用）
    /// </summary>
    [HttpPost("verify-pin")]
    public async Task<IActionResult> VerifyPin(Guid cuid, [FromBody] VerifyPinRequest request)
    {
        var client = await DbContext.Clients.FindAsync(cuid);
        if (client == null)
            return NotFound(new Error("客户端不存在"));

        if (client.RemoteAssistPin == request.Pin)
        {
            return Ok(new { valid = true });
        }
        return Ok(new { valid = false });
    }

    /// <summary>
    /// 禁用远程协助（管理端调用）
    /// </summary>
    [HttpPost("admin-disable")]
    public async Task<IActionResult> AdminDisable(Guid cuid)
    {
        var client = await DbContext.Clients.FindAsync(cuid);
        if (client == null)
            return NotFound(new Error("客户端不存在"));

        client.RemoteAssistEnabled = false;
        client.RemoteAssistPin = null;
        client.UpdatedTime = DateTime.Now;
        await DbContext.SaveChangesAsync();

        Logger.LogInformation("管理员禁用客户端 {Cuid} 的远程协助", cuid);
        return Ok(new { success = true });
    }

    /// <summary>
    /// 重置远程协助 PIN（管理端调用）
    /// </summary>
    [HttpPost("reset-pin")]
    public async Task<IActionResult> ResetPin(Guid cuid)
    {
        var client = await DbContext.Clients.FindAsync(cuid);
        if (client == null)
            return NotFound(new Error("客户端不存在"));

        var newPin = Random.Shared.Next(100000, 999999).ToString();
        client.RemoteAssistPin = newPin;
        client.UpdatedTime = DateTime.Now;
        await DbContext.SaveChangesAsync();

        Logger.LogInformation("管理员重置客户端 {Cuid} 的远程协助 PIN", cuid);
        return Ok(new { success = true, pin = newPin });
    }
}

public class EnableRemoteAssistRequest
{
    public string Pin { get; set; } = "";
}

public class VerifyPinRequest
{
    public string Pin { get; set; } = "";
}
