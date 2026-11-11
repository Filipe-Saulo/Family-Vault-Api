using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Diagnostics;

namespace FamilyVaultApi.Mcp
{
    public static class McpRateLimit
    {
        public const string PolicyName = "mcp";
    }

    // Uma linha de log por chamada de ferramenta: nome, usuário, token, duração e se terminou em erro
    public static class McpToolLogging
    {
        public static McpRequestHandler<CallToolRequestParams, CallToolResult> Filter(
            McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        {
            return async (context, cancellationToken) =>
            {
                var logger = context.Services?.GetService<ILoggerFactory>()?.CreateLogger("FamilyVaultApi.Mcp.Tools");
                var tool = context.Params?.Name ?? "?";
                var userId = context.User?.FindFirst("uid")?.Value ?? "?";
                var tokenId = context.User?.FindFirst(McpTokenAuthenticationHandler.TokenIdClaim)?.Value ?? "?";
                var stopwatch = Stopwatch.StartNew();
                var isError = true;

                try
                {
                    var result = await next(context, cancellationToken);
                    isError = result.IsError == true;
                    return result;
                }
                finally
                {
                    logger?.LogInformation(
                        "MCP tool {Tool} user {UserId} token {TokenId} em {ElapsedMs} ms (erro: {IsError})",
                        tool, userId, tokenId, stopwatch.ElapsedMilliseconds, isError);
                }
            };
        }
    }
}
