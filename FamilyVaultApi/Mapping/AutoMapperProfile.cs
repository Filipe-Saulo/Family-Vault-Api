using AutoMapper;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;
using FamilyVaultApi.Models.Dto.Responses.User;

namespace FamilyVaultApi.Mapping
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {

            CreateMap<User, UserResponseDto>()
                .ForMember(dest => dest.FullName,
                    opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
                .ForMember(dest => dest.Age,
                    opt => opt.MapFrom(src => src.Age))
                .ForMember(dest => dest.UserId,
                    opt => opt.MapFrom(src => src.Id));

            CreateMap<CreateCategoryDto, Category>()
                .ForMember(d => d.CreatedAt, opt => opt.Ignore())
                .ForMember(d => d.UpdatedAt, opt => opt.Ignore());



            CreateMap<CategoryPurpose, CategoryPurposeSimpleDto>();


            CreateMap<Category, CategoryResponseDto>()
            .ForMember(d => d.Purpose, opt => opt.MapFrom(src => src.Purpose));

            CreateMap<CreateTransactionDto, Transaction>()
                .ForMember(d => d.CreatedAt, opt => opt.Ignore())
                .ForMember(d => d.UpdatedAt, opt => opt.Ignore());


            CreateMap<CategoryPurpose, CategoryPurposeSimpleDto>();
            CreateMap<Category, CategorySimpleDto>()
            .ForMember(d => d.Purpose, opt => opt.MapFrom(src => src.Purpose));


            CreateMap<TransactionType, TransactionTypeSimpleDto>();


            CreateMap<Transaction, TransactionResponseDto>()
            .ForMember(d => d.Category, opt => opt.MapFrom(src => src.Category))
            .ForMember(d => d.TransactionType, opt => opt.MapFrom(src => src.TransactionType));
        }
    }
    
}
