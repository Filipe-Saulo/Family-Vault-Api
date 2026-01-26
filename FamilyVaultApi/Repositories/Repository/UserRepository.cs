using AutoMapper;
using AutoMapper.QueryableExtensions;
using FamilyVaultApi.Data;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Repositories.IRepository;
using Microsoft.EntityFrameworkCore;

namespace FamilyVaultApi.Repositories.Repository
{
    public class UserRepository : IUserRepository
    {

        private readonly IMapper _mapper;
        private readonly DatabaseContext _context;

        public UserRepository(IMapper mapper, DatabaseContext context)
        {
            _mapper = mapper;
            _context = context;
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


        public async Task DeleteAsync(string userId)
        {
            var entity = await _context.Users.FindAsync(userId);
            if (entity == null)
                throw new KeyNotFoundException("Usuário não encontrado.");


            _context.Users.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}
