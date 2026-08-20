using FamilyVaultApi.Common;
using FamilyVaultApi.Exceptions;
using System.Net;
using System.Security;
using System.Text.Json;

namespace FamilyVaultApi.Middleware
{
    public class ExceptionMiddleware
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            this._next = next;
            this._logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var traceId = context.TraceIdentifier;

            try
            {
                await _next(context);
            }
            catch (BadRequestException ex)
            {
                _logger.LogWarning(ex, "Erro de requisição inválida. TraceId={TraceId}", traceId);
                await HandleExceptionAsync(context, HttpStatusCode.BadRequest, ex.Message, traceId);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Acesso não autorizado. TraceId={TraceId}", traceId);
                await HandleExceptionAsync(context, HttpStatusCode.Unauthorized, ex.Message, traceId);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Recurso não encontrado. TraceId={TraceId}", traceId);
                await HandleExceptionAsync(context, HttpStatusCode.NotFound, ex.Message, traceId);
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning(ex, "Recurso não encontrado. TraceId={TraceId}", traceId);
                await HandleExceptionAsync(context, HttpStatusCode.NotFound, ex.Message, traceId);
            }
            catch (SecurityException ex)
            {
                _logger.LogWarning(ex, "Acesso proibido. TraceId={TraceId}", traceId);
                await HandleExceptionAsync(context, HttpStatusCode.Forbidden, ex.Message, traceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado. TraceId={TraceId}", traceId);
                await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, StandardMessages.Get(HttpStatusCode.InternalServerError), traceId);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, HttpStatusCode statusCode, string message, string traceId)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var response = new ApiResponse<string>(message)
            {
                TraceId = traceId
            };
            var json = JsonSerializer.Serialize(response, SerializerOptions);

            await context.Response.WriteAsync(json);
        }
    }
}
