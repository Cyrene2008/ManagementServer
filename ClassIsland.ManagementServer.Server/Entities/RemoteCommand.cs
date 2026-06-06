using System.ComponentModel.DataAnnotations;
using ClassIsland.ManagementServer.Server.Abstractions.Entities;

namespace ClassIsland.ManagementServer.Server.Entities;

/// <summary>
/// 远程命令
/// </summary>
public class RemoteCommand : IObjectWithTime
{
    /// <summary>
    /// 命令 ID
    /// </summary>
    [Key]
    public long Id { get; set; }

    /// <summary>
    /// 目标客户端 CUID
    /// </summary>
    public Guid ClientCuid { get; set; }

    /// <summary>
    /// 要执行的命令
    /// </summary>
    public string Command { get; set; } = "";

    /// <summary>
    /// Shell 类型: 0=Cmd, 1=PowerShell
    /// </summary>
    public int Shell { get; set; } = 0;

    /// <summary>
    /// 命令状态: 0=Pending, 1=Running, 2=Completed, 3=Failed, 4=Timeout
    /// </summary>
    public int Status { get; set; } = 0;

    /// <summary>
    /// 退出码
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// 标准输出
    /// </summary>
    public string? Stdout { get; set; }

    /// <summary>
    /// 标准错误
    /// </summary>
    public string? Stderr { get; set; }

    /// <summary>
    /// 超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 完成时间
    /// </summary>
    public DateTime? CompletedTime { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 上次修改时间
    /// </summary>
    public DateTime UpdatedTime { get; set; } = DateTime.Now;
}
