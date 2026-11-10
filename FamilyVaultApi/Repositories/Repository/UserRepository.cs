using AutoMapper;
using AutoMapper.QueryableExtensions;
using FamilyVaultApi.Common;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Models.Internal.Enums;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FamilyVaultApi.Repositories.Repository
{
    public class UserRepository : IUserRepository
    {

        private readonly IMapper _mapper;
        private readonly DatabaseContext _context;
        private readonly UserManager<User> _userManager;

        public UserRepository(IMapper mapper, DatabaseContext context, UserManager<User> userManager)
        {
            _mapper = mapper;
            _context = context;
            _userManager = userManager;
        }



        public async Task<PagedResult<UserResponseDto>> GetAllUsersAsync(UserQueryRequestDto query)
        {
            

            IQueryable<User> usersQuery = _context.Users;

            if (!string.IsNullOrWhiteSpace(query.FirstName))
                usersQuery = usersQuery.Where(u => u.FirstName.Contains(query.FirstName));

            if (!string.IsNullOrWhiteSpace(query.FullName))
                usersQuery = usersQuery.Where(u => u.FullName.Contains(query.FullName));

            if (!string.IsNullOrWhiteSpace(query.PhoneNumber))
                usersQuery = usersQuery.Where(u => u.PhoneNumber.Contains(query.PhoneNumber));

            if (!string.IsNullOrWhiteSpace(query.Email))
                usersQuery = usersQuery.Where(u => u.Email.Contains(query.Email));

            if (!string.IsNullOrWhiteSpace(query.Search))
                usersQuery = usersQuery.Where(u =>
                    u.FullName.Contains(query.Search) ||
                    u.Email.Contains(query.Search) ||
                    u.PhoneNumber.Contains(query.Search));

            var totalCount = await usersQuery.CountAsync();

            var items = await usersQuery
                .OrderBy(u => u.FullName)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ProjectTo<UserResponseDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            await FillRolesAsync(items);

            return new PagedResult<UserResponseDto>
            {
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Items = items
            };
        }


        public async Task<UserResponseDto> GetByIdAsync(string userId)
        {
            var user = await _context.Users
                .Where(u => u.Id == userId)
                .ProjectTo<UserResponseDto>(_mapper.ConfigurationProvider)
                .FirstOrDefaultAsync();

            if (user == null)
                throw new NotFoundException("Usuário", userId);

            await FillRolesAsync(new[] { user });

            return user;
        }

        public async Task<UserResponseDto> UpdateAsync(string userId, UpdateUserDto dto)
        {
            var entity = await _context.Users.FindAsync(userId);
            if (entity == null)
                throw new NotFoundException("Usuário", userId);

            entity.FirstName = dto.FirstName;
            entity.LastName = dto.LastName;
            entity.FullName = $"{dto.FirstName} {dto.LastName}";
            entity.Age = dto.Age;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = _mapper.Map<UserResponseDto>(entity);
            await FillRolesAsync(new[] { updated });

            return updated;
        }

        // O login procura a conta pelo UserName, então o papel e o UserName mudam juntos.
        public async Task ChangeRoleAsync(User user, string role, string userName, string? email, string? phoneNumber)
        {
            // EnableRetryOnFailure exige que a transação manual rode dentro da execution strategy.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Numa nova tentativa, descarta as entidades da tentativa desfeita pelo rollback (senão gravam de novo)
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync();

                // Numa nova tentativa, descarta o ConcurrencyStamp da tentativa desfeita pelo rollback
                await _context.Entry(user).ReloadAsync();

                if (email != null)
                    EnsureSucceeded(await _userManager.SetEmailAsync(user, email));

                if (phoneNumber != null)
                    EnsureSucceeded(await _userManager.SetPhoneNumberAsync(user, phoneNumber));

                EnsureSucceeded(await _userManager.SetUserNameAsync(user, userName));
                EnsureSucceeded(await _userManager.RemoveFromRolesAsync(user, await _userManager.GetRolesAsync(user)));
                EnsureSucceeded(await _userManager.AddToRoleAsync(user, role));

                user.UpdatedAt = DateTime.UtcNow;
                EnsureSucceeded(await _userManager.UpdateAsync(user));

                await transaction.CommitAsync();
            });
        }

        private static void EnsureSucceeded(IdentityResult result)
        {
            if (!result.Succeeded)
                throw new BadRequestException(string.Join(" | ", result.Errors.Select(e => $"{e.Code}: {e.Description}")));
        }

        private async Task FillRolesAsync(IReadOnlyCollection<UserResponseDto> users)
        {
            var userIds = users.Select(u => u.UserId).ToList();

            var roles = await (
                from userRole in _context.UserRoles
                join role in _context.Roles on userRole.RoleId equals role.Id
                where userIds.Contains(userRole.UserId)
                select new { userRole.UserId, role.Name }
            ).ToListAsync();

            foreach (var user in users)
                user.Role = roles.FirstOrDefault(r => r.UserId == user.UserId)?.Name;
        }

        public async Task DeleteAsync(string userId)
        {
            var entity = await _context.Users.FindAsync(userId);
            if (entity == null)
                throw new KeyNotFoundException("Usuário não encontrado.");

            // Ordem explícita: fk_transactions_category é Restrict e as outras duas FKs são Cascade a partir do usuário.
            // EnableRetryOnFailure exige que a transação manual rode dentro da execution strategy.
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Numa nova tentativa, descarta as entidades da tentativa desfeita pelo rollback (senão gravam de novo)
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync();

                await _context.Transactions.Where(t => t.UserId == userId).ExecuteDeleteAsync();
                await _context.Categories.Where(c => c.UserId == userId).ExecuteDeleteAsync();

                _context.Users.Remove(entity);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            });
        }

        public async Task<bool> IsLastAdministratorAsync(string userId)
        {
            var administrators = await _userManager.GetUsersInRoleAsync("Administrator");

            return administrators.Count == 1 && administrators[0].Id == userId;
        }

        public async Task<bool> IsAdministratorAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("Usuário", userId);

            return await _userManager.IsInRoleAsync(user, "Administrator");
        }

        public async Task<List<PermissionCode>> GetPermissionsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("Usuário", userId);

            var claims = await _userManager.GetClaimsAsync(user);

            return claims
                .Where(c => c.Type == AppClaimTypes.Permission)
                .Select(c => Enum.Parse<PermissionCode>(c.Value))
                .ToList();
        }

        public async Task GrantPermissionAsync(string userId, PermissionCode permission)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("Usuário", userId);

            var existingClaims = await _userManager.GetClaimsAsync(user);
            var permissionValue = permission.ToString();

            if (existingClaims.Any(c => c.Type == AppClaimTypes.Permission && c.Value == permissionValue))
                throw new BadRequestException($"O usuário já possui a permissão '{permissionValue}'.");

            await _userManager.AddClaimAsync(user, new Claim(AppClaimTypes.Permission, permissionValue));
        }

        public async Task RevokePermissionAsync(string userId, PermissionCode permission)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("Usuário", userId);

            var permissionValue = permission.ToString();
            var existingClaims = await _userManager.GetClaimsAsync(user);
            var claim = existingClaims.FirstOrDefault(c => c.Type == AppClaimTypes.Permission && c.Value == permissionValue);

            if (claim == null)
                throw new NotFoundException("Permissão", permissionValue);

            await _userManager.RemoveClaimAsync(user, claim);
        }
    }
}
