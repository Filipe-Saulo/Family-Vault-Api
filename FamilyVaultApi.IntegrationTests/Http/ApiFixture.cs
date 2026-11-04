using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // Uma API (factory) por classe de teste, com o admin semeado. Cada teste cria os próprios
    // dados, então a ordem dos testes não importa. Os tokens ficam guardados porque o rate
    // limit do login é de 5 por minuto por API.
    public class ApiFixture : IDisposable
    {
        public const string AdminEmail = "admin@integration.test";
        public const string AdminPassword = "Senha@Forte123";
        public const string UserPassword = "Senha@User123";

        private static int _phoneSequence;
        private readonly SemaphoreSlim _adminLock = new(1, 1);
        private readonly SemaphoreSlim _userLock = new(1, 1);
        private readonly Dictionary<int, (string UserId, string Token)> _users = new();
        private string? _adminToken;

        public FamilyVaultApiFactory Factory { get; } = new(AdminEmail, AdminPassword);

        public HttpClient AnonymousClient() => Factory.CreateClient();

        public async Task<HttpClient> AdminClientAsync()
        {
            await _adminLock.WaitAsync();
            try
            {
                _adminToken ??= (await LoginAsync(new { email = AdminEmail, password = AdminPassword })).Token;
                return ClientWithToken(_adminToken);
            }
            finally
            {
                _adminLock.Release();
            }
        }

        // Usuários logados reaproveitados dentro da classe (vaga 0 e 1): o login tem rate limit.
        // Os dados de cada teste continuam próprios (ids e descrições únicos).
        public async Task<(string UserId, HttpClient Client)> UserClientAsync(int slot)
        {
            await _userLock.WaitAsync();
            try
            {
                if (!_users.TryGetValue(slot, out var user))
                {
                    var (_, phone) = await NewUserAsync();
                    var auth = await LoginAsync(new { phone, password = UserPassword });
                    user = (auth.UserId, auth.Token);
                    _users[slot] = user;
                }

                return (user.UserId, ClientWithToken(user.Token));
            }
            finally
            {
                _userLock.Release();
            }
        }

        // O admin cria um User por telefone (POST /api/user), sem login
        public async Task<(string UserId, string Phone)> NewUserAsync()
        {
            var phone = $"+55119{87650000 + Interlocked.Increment(ref _phoneSequence)}";
            var admin = await AdminClientAsync();

            var created = await admin.PostAsJsonAsync("/api/user", new
            {
                firstName = "Usuario",
                lastName = "Teste",
                age = 30,
                password = UserPassword,
                passwordConfirm = UserPassword,
                role = "User",
                phoneNumber = phone
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);

            var envelope = await created.ReadEnvelopeAsync<CreatedUser>();
            return (envelope.Data!.UserId, phone);
        }

        public void Dispose() => Factory.Dispose();

        private async Task<AuthResult> LoginAsync(object body)
        {
            var response = await Factory.CreateClient().PostAsJsonAsync("/api/app/login", body);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var envelope = await response.ReadEnvelopeAsync<AuthResult>();
            return envelope.Data!;
        }

        private HttpClient ClientWithToken(string token)
        {
            var client = Factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private sealed record AuthResult(string UserId, string Token, string? RefreshToken);

        private sealed record CreatedUser(string UserId);
    }
}
