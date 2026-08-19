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

            var totalCount = await usersQuery.CountAsync();

            var items = await usersQuery
                .OrderBy(u => u.FullName)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ProjectTo<UserResponseDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            return new PagedResult<UserResponseDto>
            {
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                Items = items
            };
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

            return _mapper.Map<UserResponseDto>(entity);
        }

        public async Task DeleteAsync(string userId)
        {
            var entity = await _context.Users.FindAsync(userId);
            if (entity == null)
                throw new KeyNotFoundException("Usuário não encontrado.");


            _context.Users.Remove(entity);
            await _context.SaveChangesAsync();
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
