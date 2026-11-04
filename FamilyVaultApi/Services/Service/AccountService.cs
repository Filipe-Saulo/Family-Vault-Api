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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITokenService _tokenService;
        private readonly IPhoneNumberService _phoneNumberService;

        public AccountService(IAccountRepository repository, IIdentityService identityService, IHttpContextAccessor httpContextAccessor, ITokenService tokenService, IPhoneNumberService phoneNumberService)
        {
            _repository = repository;
            _identityService = identityService;
            _httpContextAccessor = httpContextAccessor;
            _tokenService = tokenService;
            _phoneNumberService = phoneNumberService;
        }

        private async Task<string?> ValidatePhoneAsync(CreateAccountRequestDto dto, List<IdentityError> errors)
        {
            if (string.IsNullOrEmpty(dto.PhoneNumber)) return null;

            if (!_phoneNumberService.TryValidateAndFormat(dto.PhoneNumber, "BR", out var formatted))
            {
                errors.Add(new IdentityError { Code = "InvalidPhone", Description = "Telefone inválido." });
                return null;
            }

            if (await _repository.PhoneExistsAsync(formatted!))
            {
                errors.Add(new IdentityError { Code = "DuplicatePhoneNumber", Description = "Número já cadastrado." });
                return null;
            }

            return formatted;
        }

        public async Task<IEnumerable<IdentityError>> RegisterAsync(CreateAccountRequestDto dto)
        {
            // O cadastro público cria só User (telefone); Administrator é criado via POST /api/user
            if (!string.IsNullOrEmpty(dto.Email))
                throw new BadRequestException("O cadastro público é feito só com telefone.");

            var errors = new List<IdentityError>();

            if (string.IsNullOrEmpty(dto.PhoneNumber))
                errors.Add(new IdentityError { Code = "NoCredentials", Description = "Número de telefone é obrigatório." });

            var phoneFormated = await ValidatePhoneAsync(dto, errors);

            if (dto.Password != dto.PasswordConfirm)
                errors.Add(new IdentityError { Code = "PasswordMismatch", Description = "As senhas não conferem." });

            if (errors.Any())
                return errors;

            return await _identityService.RegisterUser(dto, phoneFormated);
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
            var refreshToken = await _identityService.CreateRefreshTokenAsync(user, client);

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

            var identifier = isAdmin ? user.Email : user.PhoneNumber;

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

            var isValidRefreshToken = await _identityService.VerifyRefreshTokenAsync(user, client, request.RefreshToken);
            if (!isValidRefreshToken)
            {
                await _identityService.RevokeRefreshTokensAsync(user);
                throw new UnauthorizedAccessException("Refresh token inválido ou expirado.");
            }

            var (isAdmin, isUser) = await _identityService.GetRolesAsync(user);

            var newAccessToken = await BuildAccessTokenAsync(user, isAdmin, isUser);
            var newRefreshToken = await _identityService.CreateRefreshTokenAsync(user, client);

            return new AuthResponseDto
            {
                Token = newAccessToken,
                UserId = user.Id,
                RefreshToken = newRefreshToken
            };
        }

        public async Task LogoutAsync(string? token = null)
        {
            var context = _httpContextAccessor.HttpContext;

            string? userId = null;

            if (context?.User.Identity?.IsAuthenticated == true)
            {
                userId = context.User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
            }

            if (string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(token))
            {
                var principal = _tokenService.GetPrincipalFromExpiredToken(token);
                userId = principal?.FindFirst("uid")?.Value;
            }

            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Token inválido ou ausente.");

            await _identityService.LogoutAsync(userId);
        }
    }
}
