using FamilyVaultApi.Common;
using FamilyVaultApi.Common.Validators;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Identity;
using System.Security;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class UserService : IUserService
    {

        private readonly IUserRepository _userRepository;
        private readonly IIdentityService _identityService;
        private readonly IAccountRepository _accountRepository;
        private readonly IPhoneNumberService _phoneNumberService;

        public UserService(IUserRepository userRepository, IIdentityService identityService, IAccountRepository accountRepository, IPhoneNumberService phoneNumberService)
        {
            _userRepository = userRepository;
            _identityService = identityService;
            _accountRepository = accountRepository;
            _phoneNumberService = phoneNumberService;
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserRequestDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var errors = new List<IdentityError>();

            if (dto.Password != dto.PasswordConfirm)
                errors.Add(new IdentityError { Code = "PasswordMismatch", Description = "As senhas não conferem." });

            var account = new CreateAccountRequestDto
            {
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Password = dto.Password,
                PasswordConfirm = dto.PasswordConfirm,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Age = dto.Age
            };

            string login;

            if (dto.Role == "Administrator")
            {
                if (!string.IsNullOrEmpty(dto.PhoneNumber))
                    throw new BadRequestException("Administrador é cadastrado só com e-mail.");

                if (string.IsNullOrEmpty(dto.Email))
                    throw new BadRequestException("O e-mail é obrigatório para Administrator.");

                if (!EmailValidator.Validar(dto.Email))
                    errors.Add(new IdentityError { Code = "InvalidEmail", Description = "E-mail inválido." });

                if (await _accountRepository.EmailUserExistsAsync(dto.Email))
                    errors.Add(new IdentityError { Code = "DuplicateEmail", Description = "E-mail já cadastrado." });

                login = dto.Email;

                if (!errors.Any())
                    errors.AddRange(await _identityService.RegisterAdmin(account));
            }
            else if (dto.Role == "User")
            {
                if (string.IsNullOrEmpty(dto.PhoneNumber))
                    throw new BadRequestException("O telefone é obrigatório para User.");

                if (!_phoneNumberService.TryValidateAndFormat(dto.PhoneNumber, "BR", out var formatted))
                    throw new BadRequestException("Telefone inválido.");

                if (await _accountRepository.PhoneExistsAsync(formatted!))
                    errors.Add(new IdentityError { Code = "DuplicatePhoneNumber", Description = "Número já cadastrado." });

                if (!string.IsNullOrEmpty(dto.Email) && !EmailValidator.Validar(dto.Email))
                    errors.Add(new IdentityError { Code = "InvalidEmail", Description = "E-mail inválido." });

                if (!string.IsNullOrEmpty(dto.Email) && await _accountRepository.EmailUserExistsAsync(dto.Email))
                    errors.Add(new IdentityError { Code = "DuplicateEmail", Description = "E-mail já cadastrado." });

                login = formatted!;

                if (!errors.Any())
                    errors.AddRange(await _identityService.RegisterUser(account, formatted!));
            }
            else
            {
                throw new BadRequestException("O papel deve ser Administrator ou User.");
            }

            if (errors.Any())
                throw new BadRequestException(string.Join(" | ", errors.Select(e => $"{e.Code}: {e.Description}")));

            var created = await _identityService.FindByNameAsync(login);
            if (created == null)
                throw new NotFoundException("Usuário", login);

            return await _userRepository.GetByIdAsync(created.Id);
        }


        public async Task<PagedResult<UserResponseDto>> GetUsersAsync(UserQueryRequestDto query)
        {

            if (query.PageNumber <= 0) query.PageNumber = 1;
            if (query.PageSize <= 0) query.PageSize = 20;
            else if (query.PageSize > 100) query.PageSize = 100;

            return await _userRepository.GetAllUsersAsync(query);
        }

        public async Task<UserResponseDto> GetMeAsync(ClaimsPrincipal userClaims)
        {
            var currentUserId = userClaims?.FindFirst("uid")?.Value;
            if (string.IsNullOrWhiteSpace(currentUserId))
                throw new UnauthorizedAccessException("Usuário não autenticado.");

            return await _userRepository.GetByIdAsync(currentUserId);
        }

        public async Task<UserResponseDto> UpdateUserAsync(string userId, UpdateUserDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (userClaims == null || !userClaims.Identity.IsAuthenticated)
                throw new UnauthorizedAccessException("Usuário não autenticado.");

            await EnsureCanModifyUserAsync(userId, userClaims);

            return await _userRepository.UpdateAsync(userId, dto);
        }

        public async Task DeleteUserAsync(string userId, ClaimsPrincipal userClaims)
        {
            if (userClaims == null || !userClaims.Identity.IsAuthenticated)
                throw new UnauthorizedAccessException("Usuário não autenticado.");

            await EnsureCanModifyUserAsync(userId, userClaims);

            if (await _userRepository.IsLastAdministratorAsync(userId))
                throw new ConflictException("Não é possível excluir o último administrador.");

            await _userRepository.DeleteAsync(userId);
        }

        public async Task<UserResponseDto> ChangeRoleAsync(string userId, ChangeUserRoleDto dto, ClaimsPrincipal userClaims)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.Role != "Administrator" && dto.Role != "User")
                throw new BadRequestException("O papel deve ser Administrator ou User.");

            if (userClaims.FindFirst("uid")?.Value == userId)
                throw new BadRequestException("Você não pode alterar o próprio papel.");

            var user = await _identityService.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("Usuário", userId);

            var (isAdmin, isUser) = await _identityService.GetRolesAsync(user);
            var toAdmin = dto.Role == "Administrator";
            var alreadyInRole = toAdmin ? isAdmin : (isUser && !isAdmin);

            if (alreadyInRole)
                return await _userRepository.GetByIdAsync(userId);

            if (isAdmin && await _userRepository.IsLastAdministratorAsync(userId))
                throw new ConflictException("Não é possível rebaixar o último administrador.");

            string? newEmail = null;
            string? newPhone = null;
            string login;

            if (toAdmin)
            {
                if (string.IsNullOrEmpty(user.Email))
                    newEmail = await ValidateNewEmailAsync(dto.Email);

                login = newEmail ?? user.Email!;
            }
            else
            {
                if (string.IsNullOrEmpty(user.PhoneNumber))
                    newPhone = await ValidateNewPhoneAsync(dto.PhoneNumber);

                login = newPhone ?? user.PhoneNumber!;
            }

            await _userRepository.ChangeRoleAsync(user, dto.Role, login, newEmail, newPhone);
            await _identityService.RevokeRefreshTokensAsync(user);

            return await _userRepository.GetByIdAsync(userId);
        }

        private async Task<string> ValidateNewEmailAsync(string? email)
        {
            // vira UserName: com espaço sobrando, o login por e-mail não encontraria a conta
            email = email?.Trim();

            if (string.IsNullOrEmpty(email))
                throw new BadRequestException("O e-mail é obrigatório para Administrator.");

            if (!EmailValidator.Validar(email))
                throw new BadRequestException("E-mail inválido.");

            if (await _accountRepository.EmailUserExistsAsync(email))
                throw new BadRequestException("E-mail já cadastrado.");

            return email;
        }

        private async Task<string> ValidateNewPhoneAsync(string? phone)
        {
            if (string.IsNullOrEmpty(phone))
                throw new BadRequestException("O telefone é obrigatório para User.");

            if (!_phoneNumberService.TryValidateAndFormat(phone, "BR", out var formatted))
                throw new BadRequestException("Telefone inválido.");

            if (await _accountRepository.PhoneExistsAsync(formatted!))
                throw new BadRequestException("Número já cadastrado.");

            return formatted!;
        }

        // Administrator mexe em qualquer usuário; ManageUsers, em si e em contas que não são Administrator; os demais, só em si.
        private async Task EnsureCanModifyUserAsync(string userId, ClaimsPrincipal userClaims)
        {
            var currentUserId = userClaims.FindFirst("uid")?.Value;

            if (userClaims.IsInRole("Administrator") || currentUserId == userId)
                return;

            var hasManagePermission = userClaims.HasClaim(AppClaimTypes.Permission, nameof(PermissionCode.ManageUsers));
            if (!hasManagePermission || await _userRepository.IsAdministratorAsync(userId))
                throw new SecurityException("Você não tem permissão para acessar este recurso.");
        }

        public async Task<List<PermissionCode>> GetPermissionsAsync(string userId)
        {
            return await _userRepository.GetPermissionsAsync(userId);
        }

        public async Task GrantPermissionAsync(string userId, PermissionCode permission)
        {
            // o JSON aceita qualquer número (ex.: 99); sem isso ele viraria claim
            if (!Enum.IsDefined(permission))
                throw new BadRequestException("Permissão inválida.");

            await _userRepository.GrantPermissionAsync(userId, permission);
        }

        public async Task RevokePermissionAsync(string userId, PermissionCode permission)
        {
            await _userRepository.RevokePermissionAsync(userId, permission);
        }

    }
}
