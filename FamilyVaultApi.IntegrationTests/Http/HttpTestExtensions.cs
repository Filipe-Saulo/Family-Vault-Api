using System.Net.Http.Json;
using System.Text.Json;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // O envelope ApiResponse<T> como o cliente recebe (camelCase)
    public sealed record Envelope<T>(string Message, T? Data, string? TraceId);

    public static class HttpTestExtensions
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static async Task<Envelope<T>> ReadEnvelopeAsync<T>(this HttpResponseMessage response)
        {
            var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);
            return envelope ?? throw new InvalidOperationException("Resposta sem corpo.");
        }
    }
}
