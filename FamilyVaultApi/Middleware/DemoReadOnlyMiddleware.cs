using FamilyVaultApi.Common;

namespace FamilyVaultApi.Middleware
{
    // A conta demo é pública: qualquer escrita (inclusive upload de conta, Gmail, token do MCP e troca de senha)
    // responde 403. Fica depois do UseAuthentication, para enxergar a claim do JWT.
    public class DemoReadOnlyMiddleware
    {
        public const string Message = "Conta demo é somente leitura.";

        // Rotas de sessão continuam liberadas, para entrar, renovar e sair
        private static readonly string[] AllowedPaths =
        {
            "/api/web/login",
            "/api/app/login",
            "/api/web/refreshtoken",
            "/api/app/refreshtoken",
            "/api/logout"
        };

        private readonly RequestDelegate _next;

        public DemoReadOnlyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.IsDemo() && !IsReadOnly(context.Request))
            {
                await ApiResponseWriter.WriteAsync(context, StatusCodes.Status403Forbidden, Message);
                return;
            }

            await _next(context);
        }

        private static bool IsReadOnly(HttpRequest request) =>
            HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || AllowedPaths.Any(path => request.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
    }
}
