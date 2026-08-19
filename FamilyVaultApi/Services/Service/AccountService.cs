using FamilyVaultApi.Common.Validators;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _repository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public AccountService(IAccountRepository repository, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _httpContextAccessor = httpContextAccessor;
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
            if (!await _repository.AdministratorExistsAsync())
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
                ? await _repository.RegisterAdmin(dto)
                : await _repository.RegisterUser(dto, phoneFormated);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto loginDto)
        {
            bool isAdmin = !string.IsNullOrEmpty(loginDto.Email) && string.IsNullOrEmpty(loginDto.Phone);
            bool isUser = string.IsNullOrEmpty(loginDto.Email) && !string.IsNullOrEmpty(loginDto.Phone);

            if (string.IsNullOrEmpty(loginDto.Email) && string.IsNullOrEmpty(loginDto.Phone))
                throw new BadRequestException("Usuário não preenchido.");

            if (!isAdmin && !isUser)
                throw new BadRequestException("Telefone e E-mail estão preenchidos.");

            var authResult = await _repository.Login(loginDto);

            if (authResult == null)
                throw new UnauthorizedAccessException("Usuário inválido.");

            return new AuthResponseDto
            {
                UserId = authResult.UserId,
                Token = authResult.Token,
                RefreshToken = authResult.RefreshToken
            };
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, bool isWeb)
        {
            var handler = new JwtSecurityTokenHandler();
            var tokenContent = handler.ReadJwtToken(request.Token);

            var userId = tokenContent.Claims.FirstOrDefault(c => c.Type == "uid")?.Value
                ?? throw new SecurityTokenException("Token sem identificador de usuário");

            string userName;

            if (isWeb)
            {
                userName = tokenContent.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
                if (string.IsNullOrEmpty(userName))
                    throw new SecurityTokenException("Token sem email");
            }
            else
            {
                userName = tokenContent.Claims.FirstOrDefault(c => c.Type == "phone_number")?.Value;
                if (string.IsNullOrEmpty(userName))
                    throw new SecurityTokenException("Token sem phone_number");
            }
            return await _repository.RefreshTokenAsync(request);
        }

        public async Task LogoutAsync(string? token = null)
        {
            var context = _httpContextAccessor.HttpContext;
            bool isWeb = context?.Request.Headers["User-Agent"].ToString().Contains("Mozilla") ?? false;

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
            
            await _repository.LogoutAsync(userId);
            
            if (isWeb)
            {
                context.Response.Cookies.Delete("refreshToken", new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None
                });
            }
        }

        public async Task ResetPasswordAsync(PasswordResetRequestDto dto, ClaimsPrincipal userClaims)
        {
            if (dto.Password != dto.PasswordConfirm)
                throw new ArgumentException("A senha e a confirmação de senha não coincidem.");

            if (string.IsNullOrWhiteSpace(dto.Phone))
                throw new ArgumentException("Número de telefone é obrigatório.");

            string uid = null;
            bool isLogged = userClaims?.Identity?.IsAuthenticated == true;

            if (isLogged)
            {
                uid = userClaims.FindFirst("uid")?.Value;
            }

            await _repository.ResetPasswordAsync(dto, uid, isLogged);
        }
    }
}
