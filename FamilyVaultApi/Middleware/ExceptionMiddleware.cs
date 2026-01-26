using FamilyVaultApi.Common;
using FamilyVaultApi.Exceptions;
using System.Net;
using System.Text.Json;

namespace FamilyVaultApi.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            this._next = next;
            this._logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                try
                {
                    await _next(context);
                }
                catch (BadRequestException ex)
                {
                    _logger.LogWarning(ex, "Erro de requisição inválida");
                    await HandleExceptionAsync(context, HttpStatusCode.BadRequest, ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Acesso não autorizado");
                    await HandleExceptionAsync(context, HttpStatusCode.Unauthorized, ex.Message);
                }
                catch (KeyNotFoundException ex)
                {
                    _logger.LogWarning(ex, "Recurso não encontrado");
                    await HandleExceptionAsync(context, HttpStatusCode.NotFound, ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro inesperado");
                    await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, StandardMessages.Get(HttpStatusCode.InternalServerError));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Something Went wrong while processing {context.Request.Path}");
                await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, StandardMessages.Get(HttpStatusCode.InternalServerError));
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var response = new ApiResponse<string>(message);
            var json = JsonSerializer.Serialize(response);

            await context.Response.WriteAsync(json);
        }
    }

    public class ErrorDeatils
    {
        public string ErrorType { get; set; }
        public string ErrorMessage { get; set; }
    }
}
