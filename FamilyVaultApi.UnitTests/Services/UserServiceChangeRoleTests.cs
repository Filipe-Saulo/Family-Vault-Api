using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Dto.Responses.User;
using FamilyVaultApi.Repositories.IRepository;
using FamilyVaultApi.Services.IService;
using FamilyVaultApi.Services.Service;
using FamilyVaultApi.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using System.Security.Claims;

namespace FamilyVaultApi.UnitTests.Services
{
    public class UserServiceChangeRoleTests
    {
        private const string TargetId = "target-1";
        private const string Phone = "+5511987654321";
        private const string Email = "ana@example.com";

        private readonly Mock<IUserRepository> _repositoryMock = new();
        private readonly Mock<IIdentityService> _identityMock = new();
        private readonly Mock<IAccountRepository> _accountMock = new();
        private readonly Mock<IPhoneNumberService> _phoneMock = new();
        private readonly ClaimsPrincipal _admin = ClaimsPrincipalTestHelper.CreateAdmin();
        private readonly UserService _service;

        public UserServiceChangeRoleTests()
        {
            _service = new UserService(_repositoryMock.Object, _identityMock.Object, _accountMock.Object, _phoneMock.Object);
            _repositoryMock.Setup(x => x.GetByIdAsync(TargetId)).ReturnsAsync(new UserResponseDto { UserId = TargetId });
        }

        private User SetupTarget(bool isAdmin, string? email = null, string? phone = null)
        {
            var user = new User { Id = TargetId, Email = email, PhoneNumber = phone };
            _identityMock.Setup(x => x.FindByIdAsync(TargetId)).ReturnsAsync(user);
            _identityMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync((isAdmin, !isAdmin));
            return user;
        }

        private void VerifyNothingChanged()
        {
            _repositoryMock.Verify(
                x => x.ChangeRoleAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()),
                Times.Never);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenRoleIsInvalid_ShouldThrowBadRequest()
        {
            var act = () => _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "Root" }, _admin);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("O papel deve ser Administrator ou User.");
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenTargetIsTheCaller_ShouldThrowBadRequest()
        {
            var act = () => _service.ChangeRoleAsync("admin-1", new ChangeUserRoleDto { Role = "User", PhoneNumber = Phone }, _admin);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Você não pode alterar o próprio papel.");
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenTargetDoesNotExist_ShouldThrowNotFound()
        {
            _identityMock.Setup(x => x.FindByIdAsync(TargetId)).ReturnsAsync((User?)null);

            var act = () => _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "User" }, _admin);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Theory]
        [InlineData("Administrator", true)]
        [InlineData("User", false)]
        public async Task ChangeRoleAsync_WhenAlreadyInRole_ShouldNotChangeNorRevokeSessions(string role, bool isAdmin)
        {
            SetupTarget(isAdmin);

            var result = await _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = role }, _admin);

            result.UserId.Should().Be(TargetId);
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenDemotingLastAdministrator_ShouldThrowConflict()
        {
            SetupTarget(isAdmin: true, email: Email, phone: Phone);
            _repositoryMock.Setup(x => x.IsLastAdministratorAsync(TargetId)).ReturnsAsync(true);

            var act = () => _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "User" }, _admin);

            await act.Should().ThrowAsync<ConflictException>();
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenPromotingAccountWithEmail_ShouldUseItAsLoginAndRevokeSessions()
        {
            var user = SetupTarget(isAdmin: false, email: Email, phone: Phone);

            await _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "Administrator", Email = "outro@example.com" }, _admin);

            _repositoryMock.Verify(x => x.ChangeRoleAsync(user, "Administrator", Email, null, null), Times.Once);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(user), Times.Once);
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenPromotingAccountWithoutEmail_ShouldRequireIt()
        {
            SetupTarget(isAdmin: false, phone: Phone);

            var act = () => _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "Administrator" }, _admin);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("O e-mail é obrigatório para Administrator.");
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenNewEmailAlreadyExists_ShouldThrowBadRequest()
        {
            SetupTarget(isAdmin: false, phone: Phone);
            _accountMock.Setup(x => x.EmailUserExistsAsync(Email)).ReturnsAsync(true);

            var act = () => _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "Administrator", Email = Email }, _admin);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("E-mail já cadastrado.");
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenPromotingWithNewEmail_ShouldStoreIt()
        {
            var user = SetupTarget(isAdmin: false, phone: Phone);

            await _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "Administrator", Email = Email }, _admin);

            _repositoryMock.Verify(x => x.ChangeRoleAsync(user, "Administrator", Email, Email, null), Times.Once);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(user), Times.Once);
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenNewEmailHasSpaces_ShouldTrimIt()
        {
            var user = SetupTarget(isAdmin: false, phone: Phone);

            await _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "Administrator", Email = $"  {Email} " }, _admin);

            _repositoryMock.Verify(x => x.ChangeRoleAsync(user, "Administrator", Email, Email, null), Times.Once);
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenDemotingAccountWithoutPhone_ShouldRequireIt()
        {
            SetupTarget(isAdmin: true, email: Email);

            var act = () => _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "User" }, _admin);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("O telefone é obrigatório para User.");
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenNewPhoneIsInvalid_ShouldThrowBadRequest()
        {
            SetupTarget(isAdmin: true, email: Email);
            var formatted = (string?)null;
            _phoneMock.Setup(x => x.TryValidateAndFormat("123", "BR", out formatted)).Returns(false);

            var act = () => _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "User", PhoneNumber = "123" }, _admin);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Telefone inválido.");
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenNewPhoneAlreadyExists_ShouldThrowBadRequest()
        {
            SetupTarget(isAdmin: true, email: Email);
            var formatted = (string?)Phone;
            _phoneMock.Setup(x => x.TryValidateAndFormat("11987654321", "BR", out formatted)).Returns(true);
            _accountMock.Setup(x => x.PhoneExistsAsync(Phone)).ReturnsAsync(true);

            var act = () => _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "User", PhoneNumber = "11987654321" }, _admin);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("Número já cadastrado.");
            VerifyNothingChanged();
        }

        [Fact]
        public async Task ChangeRoleAsync_WhenDemotingWithNewPhone_ShouldStoreItInE164AndRevokeSessions()
        {
            var user = SetupTarget(isAdmin: true, email: Email);
            var formatted = (string?)Phone;
            _phoneMock.Setup(x => x.TryValidateAndFormat("11987654321", "BR", out formatted)).Returns(true);

            await _service.ChangeRoleAsync(TargetId, new ChangeUserRoleDto { Role = "User", PhoneNumber = "11987654321" }, _admin);

            _repositoryMock.Verify(x => x.ChangeRoleAsync(user, "User", Phone, null, Phone), Times.Once);
            _identityMock.Verify(x => x.RevokeRefreshTokensAsync(user), Times.Once);
        }
    }
}
