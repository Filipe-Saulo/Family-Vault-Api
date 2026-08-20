using FamilyVaultApi.Common.Validators;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal;
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

        public AccountService(IAccountRepository repository, IIdentityService identityService, IHttpContextAccessor httpContextAccessor, ITokenService tokenService)
        {
            _repository = repository;
            _identityService = identityService;
            _httpContextAccessor = httpContextAccessor;
            _tokenService = tokenService;
        }

        internal class RegisterContext
        {
            public string? Email { get; init; }
            public string? Phone { get; init; }

            public bool IsAdmin => !string.IsNullOrEmpty(Email);
            public bool IsUser => !string.IsNullOrEmpty(Phone) && string.IsNullOrEmpty(Email);
        }

        private void ValidateCredentials(RegisterContext ctx, List<IdentityError> errors)
        {
            if (string.IsNullOrEmpty(ctx.Email) && string.IsNullOrEmpty(ctx.Phone))
                errors.Add(new IdentityError { Code = "NoCredentials", Description = "Faltam credenciais." });
        }

        private async Task ValidateEmailAsync(RegisterContext ctx, List<IdentityError> errors)
        {
            if (string.IsNullOrEmpty(ctx.Email)) return;

            if (!EmailValidator.Validar(ctx.Email))
                errors.Add(new IdentityError { Code = "InvalidEmail", Description = "E-mail inválido." });

            if (await _repository.EmailUserExistsAsync(ctx.Email))
                errors.Add(new IdentityError { Code = "DuplicateEmail", Description = "E-mail já cadastrado." });
        }

        private async Task<string?> ValidatePhoneAsync(CreateAccountRequestDto dto, List<IdentityError> errors)
        {
            if (string.IsNullOrEmpty(dto.PhoneNumber)) return null;

            string phone = dto.PhoneNumber.Trim();
            string formatted;

            if (phone.StartsWith("55"))
            {
                if (!PhoneValidator.ValidarCelularBr(phone, out formatted))
                {
                    errors.Add(new IdentityError { Code = "InvalidPhone", Description = "Telefone brasileiro inválido." });
                    return null;
                }
            }
            else
            {
                if (!PhoneValidator.ValidarCelularInternacional(phone))
                {
                    errors.Add(new IdentityError { Code = "InvalidPhone", Description = "Telefone internacional inválido." });
                    return null;
                }
                formatted = phone;
            }

            if (await _repository.PhoneExistsAsync(formatted))
            {
                errors.Add(new IdentityError { Code = "DuplicatePhoneNumber", Description = "Número já cadastrado." });
                return null;
            }

            return formatted;
        }

        private async Task ValidateAdminRegistrationAllowedAsync(List<IdentityError> errors)
        {
            if (!await _identityService.AdministratorExistsAsync())
                return; // bootstrap: primeiro Administrator pode se registrar livremente

            var caller = _httpContextAccessor.HttpContext?.User;
            var callerIsAdmin = caller?.Identity?.IsAuthenticated == true && caller.IsInRole("Administrator");

            if (!callerIsAdmin)
                errors.Add(new IdentityError { Code = "AdminRegistrationRestricted", Description = "Apenas um Administrator autenticado pode cadastrar novos administradores." });
        }

        public async Task<IEnumerable<IdentityError>> RegisterAsync(CreateAccountRequestDto dto)
        {
            var errors = new List<IdentityError>();

            var ctx = new RegisterContext
            {
                Email = dto.Email,
                Phone = dto.PhoneNumber,
            };

            ValidateCredentials(ctx, errors);
            await ValidateEmailAsync(ctx, errors);
            var phoneFormated = await ValidatePhoneAsync(dto, errors);

            if (dto.Password != dto.PasswordConfirm)
                errors.Add(new IdentityError { Code = "PasswordMismatch", Description = "As senhas não conferem." });

            if (ctx.IsAdmin)
                await ValidateAdminRegistrationAllowedAsync(errors);

            if (errors.Any())
                return errors;

            // Decide fluxo
            return ctx.IsAdmin
                ? await _identityService.RegisterAdmin(dto)
                : await _identityService.RegisterUser(dto, phoneFormated);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto loginDto)
        {
            bool isAdmin = !string.IsNullOrEmpty(loginDto.Email) && string.IsNullOrEmpty(loginDto.Phone);
            bool isUser = string.IsNullOrEmpty(loginDto.Email) && !string.IsNullOrEmpty(loginDto.Phone);

            if (string.IsNullOrEmpty(loginDto.Email) && string.IsNullOrEmpty(loginDto.Phone))
                throw new BadRequestException("Usuário não preenchido.");

            if (!isAdmin && !isUser)
                throw new BadRequestException("Telefone e E-mail estão preenchidos.");

            var user = await _identityService.FindUserByLoginAsync(loginDto);
            if (user == null)
                throw new NotFoundException("Usuário não encontrado", loginDto.Email ?? loginDto.Phone);

            if (!await _identityService.CheckPasswordAsync(user, loginDto.Password))
                throw new BadRequestException("Senha inválida.");

            await _identityService.UpdateLastLoginAsync(user);

            var (userIsAdmin, userIsUser) = await _identityService.GetRolesAsync(user);
            if (!userIsAdmin && !userIsUser)
                throw new InvalidOperationException("Usuário sem role válida.");

            var token = await BuildAccessTokenAsync(user, userIsAdmin, userIsUser);
            var refreshToken = await _identityService.CreateRefreshTokenAsync(user);

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

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var handler = new JwtSecurityTokenHandler();
            var tokenContent = handler.ReadJwtToken(request.Token);

            var userId = tokenContent.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new SecurityTokenException("Token sem identificador de usuário");

            var username = tokenContent.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Email || c.Type == "phone_number")?.Value;

            if (string.IsNullOrEmpty(username))
                throw new SecurityTokenException("Token inválido.");

            var user = await _identityService.FindByNameAsync(username);
            if (user == null)
                throw new UnauthorizedAccessException("Usuário não encontrado ou não autorizado.");

            var isValidRefreshToken = await _identityService.VerifyRefreshTokenAsync(user, request.RefreshToken);
            if (!isValidRefreshToken)
            {
                await _identityService.RevokeSecurityStampAsync(user);
                throw new SecurityTokenException("Refresh token inválido ou expirado.");
            }

            var (isAdmin, isUser) = await _identityService.GetRolesAsync(user);

            var newAccessToken = await BuildAccessTokenAsync(user, isAdmin, isUser);
            var newRefreshToken = await _identityService.CreateRefreshTokenAsync(user);

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
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
            }

            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Token inválido ou ausente.");

            await _identityService.LogoutAsync(userId);
        }

        public async Task ResetPasswordAsync(PasswordResetRequestDto dto, ClaimsPrincipal userClaims)
        {
            if (dto.Password != dto.PasswordConfirm)
                throw new ArgumentException("A senha e a confirmação de senha não coincidem.");

            if (string.IsNullOrWhiteSpace(dto.Phone))
                throw new ArgumentException("Número de telefone é obrigatório.");

            var user = await _repository.FindByPhoneAsync(dto.Phone);
            if (user == null)
                throw new NotFoundException("Usuário não encontrado.", dto.Phone);

            bool isLogged = userClaims?.Identity?.IsAuthenticated == true;
            bool isAdmin = isLogged && userClaims.IsInRole("Administrator");
            string? uid = isLogged ? userClaims.FindFirst("uid")?.Value : null;

            if (isLogged && !isAdmin && uid != user.Id)
                throw new SecurityException("Você não tem permissão para acessar este recurso.");

            await _repository.UpdatePasswordAsync(user, dto.Password);
        }
    }
}
