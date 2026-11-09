using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FamilyVaultApi.Services.Service
{
    public class ResendVerificationCodeSender : IVerificationCodeSender
    {
        public const string HttpClientName = "resend";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ResendEmailOptions _options;

        public ResendVerificationCodeSender(IHttpClientFactory httpClientFactory, IOptions<ResendEmailOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
        }

        public async Task SendAsync(VerificationCodeMessage message)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
            {
                Content = JsonContent.Create(new
                {
                    from = _options.From,
                    to = new[] { message.Destination },
                    subject = VerificationCodeTexts.Subject(message.Purpose),
                    text = VerificationCodeTexts.Body(message.Purpose, message.Code)
                })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request);

            // A mensagem de erro leva só o status: o corpo do Resend pode repetir o e-mail
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"O Resend respondeu {(int)response.StatusCode}.");
        }
    }
}
