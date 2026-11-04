using AutoMapper;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Mapping;
using FamilyVaultApi.Models.Dto.Responses.CategoryPurpose;
using FamilyVaultApi.Models.Dto.Responses.TransactionType;
using FamilyVaultApi.Models.Internal.Enums;
using FluentAssertions;

namespace FamilyVaultApi.UnitTests.Services
{
    public class MapperProfileTests
    {
        private readonly IMapper _mapper;

        public MapperProfileTests()
        {
            var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>());
            _mapper = config.CreateMapper();
        }

        [Fact]
        public void Map_TransactionTypeToSimpleDto_ShouldFillCode()
        {
            var entity = new TransactionType { TransactionTypeId = 1, Code = "Expense", Description = "Despesa" };

            var dto = _mapper.Map<TransactionTypeSimpleDto>(entity);

            dto.Code.Should().Be(TransactionTypeCode.Expense);
        }

        [Fact]
        public void Map_CategoryPurposeToSimpleDto_ShouldFillCode()
        {
            var entity = new CategoryPurpose { CategoryPurposeId = 1, Code = "Income", Description = "Receita" };

            var dto = _mapper.Map<CategoryPurposeSimpleDto>(entity);

            dto.Code.Should().Be(CategoryPurposeCode.Income);
        }
    }
}
