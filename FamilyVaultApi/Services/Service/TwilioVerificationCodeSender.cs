using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FamilyVaultApi.Services.Service
{
    public class TwilioVerificationCodeSender : IVerificationCodeSender
    {
        public const string HttpClientName = "twilio";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TwilioSmsOptions _options;

        public TwilioVerificationCodeSender(IHttpClientFactory httpClientFactory, IOptions<TwilioSmsOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task SendAsync(VerificationCodeMessage message)
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(_options.AccountSid)}/Messages.json";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["To"] = message.Destination,
                    ["From"] = _options.From,
                    ["Body"] = VerificationCodeTexts.Body(message.Purpose, message.Code)
                })
            };
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request);

            // A mensagem de erro leva só os códigos: o corpo da Twilio pode repetir o telefone
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"A Twilio respondeu {(int)response.StatusCode} (código {await ReadProviderCodeAsync(response)}).");
        }

        private static async Task<string> ReadProviderCodeAsync(HttpResponseMessage response)
        {
            try
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return body.RootElement.TryGetProperty("code", out var code) ? code.ToString() : "desconhecido";
            }
            catch (JsonException)
            {
                return "desconhecido";
            }
        }
    }
}
