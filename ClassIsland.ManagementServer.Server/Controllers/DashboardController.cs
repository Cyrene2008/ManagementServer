using ClassIsland.ManagementServer.Server.Context;
using ClassIsland.ManagementServer.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClassIsland.ManagementServer.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController(
    ILogger<DashboardController> logger,
    ManagementServerContext dbContext,
    WebSocketConnectionManager wsManager) : ControllerBase
{
    public ILogger<DashboardController> Logger { get; } = logger;
    public ManagementServerContext DbContext { get; } = dbContext;
    public WebSocketConnectionManager WsManager { get; } = wsManager;

    /// <summary>
    /// 获取仪表盘概览数据
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var totalCount = await DbContext.Clients.CountAsync();
        var onlineCount = WsManager.OnlineCount;

        // 分组分布
        var groupDistribution = await DbContext.AbstractClients
            .GroupBy(a => a.GroupId)
            .Select(g => new
            {
                groupId = g.Key,
                count = g.Count()
            })
            .ToListAsync();
        
        var groupIds = groupDistribution.Select(g => g.groupId).ToList();
        var groups = await DbContext.ClientGroups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name);
        
        var groupDistributionResult = groupDistribution.Select(g => new
        {
            groupName = groups.GetValueOrDefault(g.groupId, "未知"),
            count = g.count
        }).ToList();

        // 最近 7 天审计事件趋势
        var sevenDaysAgo = DateTime.Now.AddDays(-7);
        var auditEventTrendRaw = await DbContext.AuditLogs
            .Where(x => x.CreatedTime >= sevenDaysAgo)
            .Select(x => x.CreatedTime.Date)
            .ToListAsync();
        var auditEventTrend = auditEventTrendRaw
            .GroupBy(d => d)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                count = g.Count()
            })
            .OrderBy(x => x.date)
            .ToList();

        // 最近命令
        var recentCommands = await DbContext.RemoteCommands
            .OrderByDescending(x => x.CreatedTime)
            .Take(10)
            .Select(x => new
            {
                id = x.Id,
                clientCuid = x.ClientCuid,
                command = x.Command,
                status = x.Status,
                exitCode = x.ExitCode,
                createdTime = x.CreatedTime
            })
            .ToListAsync();

        // 版本分布（从客户端 MAC 字段暂无版本信息，用 AbstractClient 的信息代替）
        // 注：当前 Client 实体没有 Version 字段，返回空
        var versionDistribution = new List<object>();

        // 策略覆盖
        var clientsWithPolicy = await DbContext.ObjectUpdates
            .Where(x => x.ObjectType == ClassIsland.ManagementServer.Server.Enums.ObjectTypes.Policy)
            .Select(x => x.TargetCuid)
            .Distinct()
            .CountAsync();

        var policyCoverage = new
        {
            totalClients = totalCount,
            coveredClients = totalCount - clientsWithPolicy,
            coverageRate = totalCount > 0 ? (double)(totalCount - clientsWithPolicy) / totalCount : 0
        };

        return Ok(new
        {
            onlineCount,
            totalCount,
            groupDistribution = groupDistributionResult,
            auditEventTrend,
            recentCommands,
            versionDistribution,
            policyCoverage
        });
    }
}
