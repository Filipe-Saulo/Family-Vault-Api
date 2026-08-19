using System.Net;

namespace FamilyVaultApi.Common
{
    public class ApiResponse<T>
    {
        public string Message { get; set; }
        public T Data { get; set; }
        public string? TraceId { get; set; }

        public ApiResponse(string message, T data = default)
        {
            Message = message;
            Data = data;
        }

        public static ApiResponse<T> Ok(T data, string customMessage = null)
            => new(customMessage ?? StandardMessages.Get(HttpStatusCode.OK), data);

        public static ApiResponse<T> Created(T data, string customMessage = null)
            => new(customMessage ?? StandardMessages.Get(HttpStatusCode.Created), data);

        public static ApiResponse<T> Accepted(string customMessage = null)
            => new(customMessage ?? StandardMessages.Get(HttpStatusCode.Accepted));

        public static ApiResponse<T> NoContent(string customMessage = null)
            => new(customMessage ?? StandardMessages.Get(HttpStatusCode.NoContent));

        public static ApiResponse<T> BadRequest(string customMessage = null)
            => new(customMessage ?? StandardMessages.Get(HttpStatusCode.BadRequest));

        public static ApiResponse<T> NotFound(string customMessage = null)
            => new(customMessage ?? StandardMessages.Get(HttpStatusCode.NotFound));

        public static ApiResponse<T> Unauthorized(string customMessage = null)
            => new(customMessage ?? StandardMessages.Get(HttpStatusCode.Unauthorized));

        public static ApiResponse<T> InternalServerError(string customMessage = null)
            => new(customMessage ?? StandardMessages.Get(HttpStatusCode.InternalServerError));
    }

}
