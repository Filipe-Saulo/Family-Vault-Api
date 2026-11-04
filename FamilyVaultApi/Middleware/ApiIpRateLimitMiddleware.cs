using AspNetCoreRateLimit;
using FamilyVaultApi.Common;
using Microsoft.Extensions.Options;

namespace FamilyVaultApi.Middleware
{
    // IpRateLimitMiddleware com o 429 no envelope ApiResponse<string> (a lib só aceita corpo fixo, sem traceId)
    public class ApiIpRateLimitMiddleware : IpRateLimitMiddleware
    {
        public ApiIpRateLimitMiddleware(
            RequestDelegate next,
            IProcessingStrategy processingStrategy,
            IOptions<IpRateLimitOptions> options,
            IIpPolicyStore policyStore,
            IRateLimitConfiguration config,
            ILogger<IpRateLimitMiddleware> logger)
            : base(next, processingStrategy, options, policyStore, config, logger)
        {
        }

        public override Task ReturnQuotaExceededResponse(HttpContext httpContext, RateLimitRule rule, string retryAfter)
        {
            if (!string.IsNullOrEmpty(retryAfter))
                httpContext.Response.Headers["Retry-After"] = retryAfter;

            return ApiResponseWriter.WriteAsync(
                httpContext,
                StatusCodes.Status429TooManyRequests,
                $"Muitas requisições. Tente novamente em {retryAfter} segundos.");
        }
    }
}
