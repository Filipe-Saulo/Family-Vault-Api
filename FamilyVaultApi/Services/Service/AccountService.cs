using FamilyVaultApi.Common.Validators;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _repository;
        private readonly IIdentityService _identityService;
        private readonly ITokenService _tokenService;
        private readonly IPhoneNumberService _phoneNumberService;
        private readonly IRefreshTokenService _refreshTokenService;

        private readonly IRegistrationCodeStore _registrationCodeStore;
        private readonly IVerificationSendLimiter _sendLimiter;
        private readonly IVerificationCodeSender _codeSender;

        public AccountService(IAccountRepository repository, IIdentityService identityService, ITokenService tokenService, IPhoneNumberService phoneNumberService, IRefreshTokenService refreshTokenService,
            IRegistrationCodeStore registrationCodeStore, IVerificationSendLimiter sendLimiter, IVerificationCodeSender codeSender)
        {
            _repository = repository;
            _identityService = identityService;
            _tokenService = tokenService;
            _phoneNumberService = phoneNumberService;
            _refreshTokenService = refreshTokenService;
            _registrationCodeStore = registrationCodeStore;
            _sendLimiter = sendLimiter;
            _codeSender = codeSender;
        }

        // Telefone em E.164 ou e-mail em trim + minúsculas; null se o formato for inválido
        private string? FormatDestination(VerificationChannel channel, string? value)
        {
            if (channel == VerificationChannel.Email)
            {
                var email = value?.Trim() ?? string.Empty;
                return EmailValidator.Validar(email) ? EmailValidator.Normalizar(email) : null;
            }

            return _phoneNumberService.TryValidateAndFormat(value ?? string.Empty, "BR", out var phone) ? phone : null;
        }

        private async Task<bool> DestinationExistsAsync(VerificationChannel channel, string destination)
        {
            if (channel == VerificationChannel.Email)
                return await _repository.EmailUserExistsAsync(destination) || await _identityService.FindByNameAsync(destination) != null;

            return await _repository.PhoneExistsAsync(destination);
        }

        public async Task SendRegistrationCodeAsync(RegistrationCodeRequestDto dto)
        {
            var hasPhone = !string.IsNullOrWhiteSpace(dto.PhoneNumber);
            var hasEmail = !string.IsNullOrWhiteSpace(dto.Email);
            if (hasPhone == hasEmail)
                throw new BadRequestException("Informe o telefone ou o e-mail, só um dos dois.");

            // Responde igual para formato inválido, conta existente ou limite estourado, para não revelar quais contas existem
            var channel = hasEmail ? VerificationChannel.Email : VerificationChannel.Sms;
            var destination = FormatDestination(channel, hasEmail ? dto.Email : dto.PhoneNumber);
            if (destination == null)
                return;

            if (await DestinationExistsAsync(channel, destination))
                return;

            if (!await _sendLimiter.TryAcquireAsync(channel, destination))
                return;

            var code = await _registrationCodeStore.IssueAsync(destination, VerificationPurpose.Registration);
            await _codeSender.SendAsync(new VerificationCodeMessage(channel, destination, code, VerificationPurpose.Registration));
        }

        public async Task<IEnumerable<IdentityError>> RegisterAsync(RegisterAccountRequestDto dto)
        {
            // O cadastro público cria só User, por telefone ou e-mail confirmado por código; Administrator é criado via POST /api/user
            var errors = new List<IdentityError>();

            var hasPhone = !string.IsNullOrWhiteSpace(dto.PhoneNumber);
            var hasEmail = !string.IsNullOrWhiteSpace(dto.Email);
            var channel = hasEmail ? VerificationChannel.Email : VerificationChannel.Sms;
            string? destination = null;

            if (hasPhone == hasEmail)
                errors.Add(new IdentityError { Code = "NoCredentials", Description = hasPhone ? "Informe só o telefone ou só o e-mail." : "Informe o telefone ou o e-mail." });
            else
                destination = ValidateDestinationFormat(channel, hasEmail ? dto.Email : dto.PhoneNumber, errors);

            if (dto.Password != dto.PasswordConfirm)
                errors.Add(new IdentityError { Code = "PasswordMismatch", Description = "As senhas não conferem." });

            if (errors.Any())
                return errors;

            if (!await _registrationCodeStore.VerifyAsync(destination!, VerificationPurpose.Registration, dto.Code))
                throw new BadRequestException("Código inválido ou expirado.");

            // Só depois do código válido: antes disso a mensagem de duplicidade revelaria quais contas existem
            if (await DestinationExistsAsync(channel, destination!))
                return new[]
                {
                    channel == VerificationChannel.Email
                        ? new IdentityError { Code = "DuplicateEmail", Description = "E-mail já cadastrado." }
                        : new IdentityError { Code = "DuplicatePhoneNumber", Description = "Número já cadastrado." }
                };

            var account = new CreateAccountRequestDto
            {
                Password = dto.Password,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                PasswordConfirm = dto.PasswordConfirm,
                Age = dto.Age
            };

            var result = (await _identityService.RegisterVerifiedUser(account, destination!, channel)).ToList();

            // O código só é gasto quando a conta é criada; senha recusada deixa tentar de novo
            if (!result.Any())
                await _registrationCodeStore.RemoveAsync(destination!, VerificationPurpose.Registration);

            return result;
        }

        private string? ValidateDestinationFormat(VerificationChannel channel, string? value, List<IdentityError> errors)
        {
            var destination = FormatDestination(channel, value);
            if (destination == null)
                errors.Add(channel == VerificationChannel.Email
                    ? new IdentityError { Code = "InvalidEmail", Description = "E-mail inválido." }
                    : new IdentityError { Code = "InvalidPhone", Description = "Telefone inválido." });

            return destination;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto loginDto, AuthClient client)
        {
            bool isAdmin = !string.IsNullOrEmpty(loginDto.Email) && string.IsNullOrEmpty(loginDto.Phone);
            bool isUser = string.IsNullOrEmpty(loginDto.Email) && !string.IsNullOrEmpty(loginDto.Phone);

            if (string.IsNullOrEmpty(loginDto.Email) && string.IsNullOrEmpty(loginDto.Phone))
                throw new BadRequestException("Usuário não preenchido.");

            if (!isAdmin && !isUser)
                throw new BadRequestException("Telefone e E-mail estão preenchidos.");

            var user = await _identityService.FindUserByLoginAsync(loginDto);
            if (user == null)
                throw new UnauthorizedAccessException("Usuário ou senha inválidos.");

            // Conta bloqueada responde igual a senha errada, para não revelar quais contas existem
            if (await _identityService.IsLockedOutAsync(user))
                throw new UnauthorizedAccessException("Usuário ou senha inválidos.");

            if (!await _identityService.CheckPasswordAsync(user, loginDto.Password))
            {
                await _identityService.AccessFailedAsync(user);
                throw new UnauthorizedAccessException("Usuário ou senha inválidos.");
            }

            await _identityService.ResetAccessFailedCountAsync(user);

            await _identityService.UpdateLastLoginAsync(user);

            var (userIsAdmin, userIsUser) = await _identityService.GetRolesAsync(user);
            if (!userIsAdmin && !userIsUser)
                throw new SecurityException("Usuário sem role válida.");

            var token = await BuildAccessTokenAsync(user, userIsAdmin, userIsUser);
            var refreshToken = await _refreshTokenService.CreateSessionAsync(user.Id, client);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Token = token,
                RefreshToken = refreshToken
            };
        }

        private async Task<string> BuildAccessTokenAsync(Data.Entities.User user, bool isAdmin, bool isUser)
        {
            var userClaims = await _identityService.GetUserClaimsAsync(user);

            var permissionClaims = new List<Claim>();
            if (isAdmin) permissionClaims.AddRange(await _identityService.GetRoleClaimsAsync("Administrator"));
            if (isUser) permissionClaims.AddRange(await _identityService.GetRoleClaimsAsync("User"));

            // User cadastrado por e-mail não tem telefone
            var identifier = isAdmin ? user.Email : user.PhoneNumber ?? user.Email;

            return _tokenService.GenerateAccessToken(new TokenClaimsData
            {
                UserId = user.Id,
                Identifier = identifier,
                IsAdmin = isAdmin,
                IsUser = isUser,
                SecurityStamp = user.SecurityStamp,
                UserClaims = userClaims,
                PermissionClaims = permissionClaims
            });
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, AuthClient client)
        {
            var principal = _tokenService.GetPrincipalFromExpiredToken(request.Token);
            if (principal == null)
                throw new UnauthorizedAccessException("Token inválido.");

            var userId = principal.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Token sem identificador de usuário");

            var user = await _identityService.FindByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("Usuário não encontrado ou não autorizado.");

            if (await _identityService.IsLockedOutAsync(user))
                throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");

            var newRefreshToken = await _refreshTokenService.RotateAsync(user.Id, client, request.RefreshToken);
            if (newRefreshToken == null)
                throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");

            var (isAdmin, isUser) = await _identityService.GetRolesAsync(user);

            var newAccessToken = await BuildAccessTokenAsync(user, isAdmin, isUser);

            return new AuthResponseDto
            {
                Token = newAccessToken,
                UserId = user.Id,
                RefreshToken = newRefreshToken
            };
        }

        // Encerra só a sessão do refresh informado. Sem token ou com token desconhecido não há o que
        // encerrar e o logout continua sendo sucesso, como na revogação do OAuth (RFC 7009).
        public async Task LogoutAsync(string? refreshToken)
        {
            if (!string.IsNullOrEmpty(refreshToken))
                await _refreshTokenService.RevokeSessionAsync(refreshToken);
        }
    }
}
