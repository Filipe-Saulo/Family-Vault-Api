using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Responses.Account;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.Repositories.Repository
{
    public class AccountRepository : IAccountRepository
    {        
        private readonly IConfiguration _configuration;
        private readonly DatabaseContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private readonly string _loginProvider = TokenOptions.DefaultProvider;
        private readonly string _refreshTokenPurpose = "RefreshToken";

        public AccountRepository(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration, DatabaseContext context)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<AuthResult> Login(LoginRequestDto loginDto)
        {         
            User user = null;
            if (!string.IsNullOrEmpty(loginDto.Email))
                user = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == loginDto.Email);
            else if (!string.IsNullOrEmpty(loginDto.Phone))
                user = await _userManager.Users.FirstOrDefaultAsync(u => u.UserName == loginDto.Phone);

            if (user == null)
                throw new NotFoundException("Usuário não encontrado", loginDto.Email ?? loginDto.Phone);
        

            var isValidPassword = await _userManager.CheckPasswordAsync(user, loginDto.Password);

            if (!isValidPassword)
            {

                throw new BadRequestException("Senha inválida.");
            }

            user.LastLogin = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
            var isUser = await _userManager.IsInRoleAsync(user, "User");


            if (!isAdmin && !isUser)
                throw new InvalidOperationException("Usuário sem role válida.");

            var token = await GenerateToken(user, isAdmin, isUser);
            var refreshToken = await CreateRefreshToken(user);



            return new AuthResult
            {
                UserId = user.Id,
                Token = token,
                RefreshToken = refreshToken,
                IsAdmin = isAdmin,
                IsUser = isUser
            };
        }

        public async Task<string> CreateRefreshToken(User user)
        {
            // Remove token antigo se existir
            await _userManager.RemoveAuthenticationTokenAsync(user, _loginProvider, _refreshTokenPurpose);


            // Gera novo token seguro
            var newRefreshToken = await _userManager.GenerateUserTokenAsync(user, _loginProvider, _refreshTokenPurpose);


            // Salva o token no banco
            await _userManager.SetAuthenticationTokenAsync(user, _loginProvider, _refreshTokenPurpose, newRefreshToken);


            return newRefreshToken;
        }

        private async Task<string> GenerateToken(User user, bool isAdmin, bool isUser)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);


            var userClaims = await _userManager.GetClaimsAsync(user);


            var identifier = isAdmin ? user.Email : user.PhoneNumber;
            var identifierClaim = isAdmin
            ? new Claim(JwtRegisteredClaimNames.Email, user.Email)
            : new Claim("phone_number", user.PhoneNumber);


            var roleClaims = new List<Claim>();
            if (isAdmin) roleClaims.Add(new Claim(ClaimTypes.Role, "Administrator"));
            if (isUser) roleClaims.Add(new Claim(ClaimTypes.Role, "User"));

            var permissionClaims = new List<Claim>();
            if (isAdmin) permissionClaims.AddRange(await _roleManager.GetClaimsAsync(await _roleManager.FindByNameAsync("Administrator")));
            if (isUser) permissionClaims.AddRange(await _roleManager.GetClaimsAsync(await _roleManager.FindByNameAsync("User")));


            var claims = new List<Claim>
            {
            new Claim(JwtRegisteredClaimNames.Sub, identifier),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            identifierClaim,
            new Claim("uid", user.Id),
            new Claim("SecurityStamp", user.SecurityStamp)
            }
            .Union(userClaims)
            .Union(roleClaims)
            .Union(permissionClaims);


            var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Convert.ToDouble(_configuration["JwtSettings:DurationInMinutes"])),
            signingCredentials: credentials
            );


            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var jwtHandler = new JwtSecurityTokenHandler();
            var tokenContent = jwtHandler.ReadJwtToken(request.Token);

            // Obtém username (email para admin, telefone para usuário)
            var username = tokenContent.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Email || c.Type == "phone_number")?.Value;

            if (string.IsNullOrEmpty(username))
                throw new SecurityTokenException("Token inválido.");

            var user = await _userManager.FindByNameAsync(username);

            if (user == null)
                throw new UnauthorizedAccessException("Usuário não encontrado ou não autorizado.");

            var isValidRefreshToken = await _userManager.VerifyUserTokenAsync(
                user,
                _loginProvider,         
                _refreshTokenPurpose,  
                request.RefreshToken
            );

            if (!isValidRefreshToken)
            {
                await _userManager.UpdateSecurityStampAsync(user);
                throw new SecurityTokenException("Refresh token inválido ou expirado.");
            }

            // Roles
            var isAdmin = await _userManager.IsInRoleAsync(user, "Administrator");
            var isUser = await _userManager.IsInRoleAsync(user, "User");

            // Gera novos tokens
            var newAccessToken = await GenerateToken(user, isAdmin, isUser);
            var newRefreshToken = await CreateRefreshToken(user); 

            return new AuthResponseDto
            {
                Token = newAccessToken,
                UserId = user.Id,
                RefreshToken = newRefreshToken
            };
        }

        public async Task<IEnumerable<IdentityError>> RegisterAdmin(CreateAccountRequestDto createAccountDto)
        {
            var user = new User
            {
                UserName = createAccountDto.Email,
                Email = createAccountDto.Email,
                FirstName = createAccountDto.FirstName,
                LastName = createAccountDto.LastName,
                FullName = $"{createAccountDto.FirstName} {createAccountDto.LastName}",
                PhoneNumber = createAccountDto.PhoneNumber,
                RegisteredAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,             
                Age = createAccountDto.Age
            };

            var resultAccount = await _userManager.CreateAsync(user, createAccountDto.Password);

            if (!resultAccount.Succeeded)
                return resultAccount.Errors;

            try
            {                
                await _userManager.AddToRoleAsync(user, "Administrator");
            }
            catch (Exception)
            {
                await _userManager.DeleteAsync(user);

                throw;
            }

            return Enumerable.Empty<IdentityError>();
        }

        public async Task<IEnumerable<IdentityError>> RegisterUser(CreateAccountRequestDto createAccountDto, string phone)
        {    

            var user = new User
            {
                UserName = phone,
                Email = createAccountDto.Email,
                FirstName = createAccountDto.FirstName,
                LastName = createAccountDto.LastName,
                FullName = $"{createAccountDto.FirstName} {createAccountDto.LastName}",
                PhoneNumber = phone,
                RegisteredAt = DateTime.UtcNow,
                LastLogin = DateTime.UtcNow,        
                Age = createAccountDto.Age
            };

            var resultAccount = await _userManager.CreateAsync(user, createAccountDto.Password);

            if (!resultAccount.Succeeded)
                return resultAccount.Errors;

            try
            {
                await _userManager.AddToRoleAsync(user, "User");
            }
            catch (Exception)
            {
                await _userManager.DeleteAsync(user);

                throw;
            }
            return Enumerable.Empty<IdentityError>();
        }


        public async Task LogoutAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("Usuário não encontrado.", userId);

            // Remove refresh token específico (se estiver usando UserManager com tokens)
            await _userManager.RemoveAuthenticationTokenAsync(user, _loginProvider, _refreshTokenPurpose);

            // Atualiza SecurityStamp para invalidar todos os tokens ativos (opcional, recomendado)
            await _userManager.UpdateSecurityStampAsync(user);
        }


        public async Task<bool> PhoneExistsAsync(string phoneNumber)
        {            
            return await _context.Users
                .AnyAsync(u => u.PhoneNumber != null && u.PhoneNumber == phoneNumber);
        }

        public async Task<bool> EmailUserExistsAsync(string email)
        {            

            return await _context.Users
                .AnyAsync(u => u.Email == email);
        }

   

        public async Task ResetPasswordAsync(PasswordResetRequestDto dto, string uid, bool isLogged)
        {
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == dto.Phone);
            if (user == null)
                throw new NotFoundException("Usuário não encontrado.", dto.Phone);

            bool isAdmin = isLogged && await _userManager.IsInRoleAsync(user, "Administrator");

            if (isLogged && !isAdmin && uid != user.Id)
                throw new SecurityException("Você não tem permissão para acessar este recurso.");                        

            // Atualiza senha
            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, dto.Password);
            user.SecurityStamp = Guid.NewGuid().ToString();
            user.UserName = user.PhoneNumber;
            user.NormalizedUserName = user.PhoneNumber.ToUpper();

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        
    }
}
