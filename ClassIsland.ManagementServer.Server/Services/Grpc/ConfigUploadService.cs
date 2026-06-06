using ClassIsland.ManagementServer.Server.Context;
using ClassIsland.ManagementServer.Server.Entities;
using ClassIsland.Shared.Protobuf.Client;
using ClassIsland.Shared.Protobuf.Enum;
using ClassIsland.Shared.Protobuf.Server;
using ClassIsland.Shared.Protobuf.Service;
using Grpc.Core;

namespace ClassIsland.ManagementServer.Server.Services.Grpc;

public class ConfigUploadService(
    ILogger<ConfigUploadService> logger,
    ManagementServerContext dbContext) : ConfigUpload.ConfigUploadBase
{
    public ILogger<ConfigUploadService> Logger { get; } = logger;
    public ManagementServerContext DbContext { get; } = dbContext;

    public override async Task<ConfigUploadScRsp> UploadConfig(ConfigUploadScReq request, ServerCallContext context)
    {
        var ret = new ConfigUploadScRsp();
        try
        {
            var cuidStr = context.RequestHeaders.GetValue("cuid");
            if (string.IsNullOrEmpty(cuidStr) || !Guid.TryParse(cuidStr, out var cuid))
            {
                ret.Retcode = Retcode.InvalidRequest;
                ret.Message = "Invalid cuid";
                return ret;
            }

            var client = await DbContext.Clients.FindAsync(cuid);
            if (client == null)
            {
                ret.Retcode = Retcode.ClientNotFound;
                ret.Message = "Client not found";
                return ret;
            }

            var snapshot = new ClientConfigSnapshot
            {
                ClientCuid = cuid,
                RequestGuidId = request.RequestGuidId ?? "",
                Payload = request.Payload ?? ""
            };

            await DbContext.ClientConfigSnapshots.AddAsync(snapshot);
            await DbContext.SaveChangesAsync();

            Logger.LogInformation("收到客户端 {Cuid} 的配置上传，请求ID: {RequestId}", cuid, request.RequestGuidId);
            ret.Retcode = Retcode.Success;
        }
        catch (Exception e)
        {
            Logger.LogError(e, "处理配置上传时出错");
            ret.Retcode = Retcode.ServerInternalError;
            ret.Message = e.Message;
        }

        return ret;
    }
}
