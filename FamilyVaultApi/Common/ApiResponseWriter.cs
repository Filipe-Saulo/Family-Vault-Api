using System.Text.Json;

namespace FamilyVaultApi.Common
{
    // Escreve o envelope ApiResponse<string> fora do ExceptionMiddleware (401/403 do JWT e 429 do rate limit)
    public static class ApiResponseWriter
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static Task WriteAsync(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse<string>(message)
            {
                TraceId = context.TraceIdentifier
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
        }
    }
}
