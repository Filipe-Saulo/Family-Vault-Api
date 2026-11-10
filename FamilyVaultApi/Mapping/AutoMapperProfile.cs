using AutoMapper;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Models.Dto.Requests.Category;
using FamilyVaultApi.Models.Dto.Responses.Charge;
using FamilyVaultApi.Models.Dto.Responses.Gmail;
using FamilyVaultApi.Models.Dto.Requests.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Requests.Transaction;
using FamilyVaultApi.Models.Dto.Requests.TransactionType;
using FamilyVaultApi.Models.Dto.Responses.Category;
using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Responses.TransactionResponse;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Models.Internal.Enums;

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
                    opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Role, opt => opt.Ignore()); // preenchido pelo UserRepository

            CreateMap<CreateCategoryDto, Category>()
                .ForMember(d => d.UserId, opt => opt.Ignore())
                .ForMember(d => d.CreatedAt, opt => opt.Ignore())
                .ForMember(d => d.UpdatedAt, opt => opt.Ignore());



            CreateMap<CategoryPurpose, CategoryPurposeSimpleDto>()
                .ForMember(d => d.Code, opt => opt.MapFrom(src => Enum.Parse<CategoryPurposeCode>(src.Code, true)));


            CreateMap<Category, CategoryResponseDto>()
            .ForMember(d => d.Purpose, opt => opt.MapFrom(src => src.Purpose))
            .ForMember(d => d.IsGlobal, opt => opt.MapFrom(src => src.UserId == null));

            CreateMap<CreateTransactionDto, Transaction>()
                .ForMember(d => d.CreatedAt, opt => opt.Ignore())
                .ForMember(d => d.UpdatedAt, opt => opt.Ignore());


            CreateMap<CategoryPurpose, CategoryPurposeSimpleDto>()
                .ForMember(d => d.Code, opt => opt.MapFrom(src => Enum.Parse<CategoryPurposeCode>(src.Code, true)));
            CreateMap<Category, CategorySimpleDto>()
            .ForMember(d => d.Purpose, opt => opt.MapFrom(src => src.Purpose));


            CreateMap<TransactionType, TransactionTypeSimpleDto>()
                .ForMember(d => d.Code, opt => opt.MapFrom(src => Enum.Parse<TransactionTypeCode>(src.Code, true)));


            CreateMap<Transaction, TransactionResponseDto>()
            .ForMember(d => d.Category, opt => opt.MapFrom(src => src.Category))
            .ForMember(d => d.TransactionType, opt => opt.MapFrom(src => src.TransactionType));

            CreateMap<CreateCategoryPurposeDto, CategoryPurpose>()
                .ForMember(d => d.Code, opt => opt.Ignore())
                .ForMember(d => d.IsActive, opt => opt.Ignore())
                .ForMember(d => d.CreatedAt, opt => opt.Ignore())
                .ForMember(d => d.UpdatedAt, opt => opt.Ignore());

            CreateMap<CategoryPurpose, CategoryPurposeResponseDto>()
                .ForMember(d => d.Code, opt => opt.MapFrom(src => Enum.Parse<CategoryPurposeCode>(src.Code, true)));

            CreateMap<Charge, ChargeResponseDto>()
                .ForMember(d => d.DocumentId, opt => opt.MapFrom(src => src.InboundDocumentId))
                .ForMember(d => d.Source, opt => opt.MapFrom(src => src.InboundDocument.Source.ToString()))
                .ForMember(d => d.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<InboundDocument, ChargeFailureResponseDto>()
                .ForMember(d => d.DocumentId, opt => opt.MapFrom(src => src.InboundDocumentId))
                .ForMember(d => d.Source, opt => opt.MapFrom(src => src.Source.ToString()));

            CreateMap<GmailConnection, GmailStatusResponseDto>()
                .ForMember(d => d.Connected, opt => opt.MapFrom(src => src.Status == GmailConnectionStatus.Active))
                .ForMember(d => d.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<GmailScan, GmailScanResponseDto>()
                .ForMember(d => d.Kind, opt => opt.MapFrom(src => src.Kind.ToString()))
                .ForMember(d => d.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<GmailIgnoredSender, GmailIgnoredSenderResponseDto>()
                .ForMember(d => d.Id, opt => opt.MapFrom(src => src.GmailIgnoredSenderId));

            CreateMap<CreateTransactionTypeDto, TransactionType>()
                .ForMember(d => d.Code, opt => opt.Ignore())
                .ForMember(d => d.IsActive, opt => opt.Ignore())
                .ForMember(d => d.CreatedAt, opt => opt.Ignore())
                .ForMember(d => d.UpdatedAt, opt => opt.Ignore());

            CreateMap<TransactionType, TransactionTypeResponseDto>()
                .ForMember(d => d.Code, opt => opt.MapFrom(src => Enum.Parse<TransactionTypeCode>(src.Code, true)));
        }
    }
    
}
