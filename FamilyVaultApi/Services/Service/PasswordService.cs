using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Services.IService;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace FamilyVaultApi.Services.Service
{
    public class PasswordService : IPasswordService
    {
        private readonly IIdentityService _identityService;
        private readonly IPhoneNumberService _phoneNumberService;
        private readonly IVerificationCodeSender _codeSender;

        public PasswordService(IIdentityService identityService, IPhoneNumberService phoneNumberService, IVerificationCodeSender codeSender)
        {
            _identityService = identityService;
            _phoneNumberService = phoneNumberService;
            _codeSender = codeSender;
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto dto)
        {
            // Responde igual para telefone inválido, conta inexistente ou bloqueada, para não revelar quais contas existem
            if (!_phoneNumberService.TryValidateAndFormat(dto.Phone, "BR", out var phone))
                return;

            var user = await _identityService.FindByPhoneAsync(phone!);
            if (user == null || await _identityService.IsLockedOutAsync(user))
                return;

            var code = await _identityService.GeneratePasswordResetCodeAsync(user);
            await _codeSender.SendAsync(phone!, code);
        }

        public async Task ResetPasswordAsync(ResetPasswordWithCodeRequestDto dto)
        {
            if (dto.Password != dto.PasswordConfirm)
                throw new BadRequestException("A senha e a confirmação de senha não coincidem.");

            if (!_phoneNumberService.TryValidateAndFormat(dto.Phone, "BR", out var phone))
                throw new BadRequestException("Código inválido ou expirado.");

            var user = await _identityService.FindByPhoneAsync(phone!);
            if (user == null || await _identityService.IsLockedOutAsync(user))
                throw new BadRequestException("Código inválido ou expirado.");

            if (!await _identityService.VerifyPasswordResetCodeAsync(user, dto.Code))
            {
                await _identityService.AccessFailedAsync(user);
                throw new BadRequestException("Código inválido ou expirado.");
            }

            ThrowIfFailed(await _identityService.ResetPasswordAsync(user, dto.Password));

            await _identityService.UnlockAsync(user);
            await _identityService.RevokeRefreshTokensAsync(user);
        }

        public async Task ChangePasswordAsync(ChangePasswordRequestDto dto, ClaimsPrincipal userClaims)
        {
            if (dto.Password != dto.PasswordConfirm)
                throw new BadRequestException("A senha e a confirmação de senha não coincidem.");

            var userId = userClaims.FindFirst("uid")?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Token sem identificador de usuário");

            var user = await _identityService.FindByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("Usuário não encontrado ou não autorizado.");

            // Conta bloqueada não chega ao Identity: impede testar a senha atual por força bruta
            if (await _identityService.IsLockedOutAsync(user))
                throw new BadRequestException("Senha atual incorreta.");

            var errors = (await _identityService.ChangePasswordAsync(user, dto.CurrentPassword, dto.Password)).ToList();
            if (errors.Any(e => e.Code == "PasswordMismatch"))
                await _identityService.AccessFailedAsync(user);

            ThrowIfFailed(errors);

            await _identityService.ResetAccessFailedCountAsync(user);

            // O cliente volta para o login
            await _identityService.RevokeRefreshTokensAsync(user);
        }

        public async Task SetUserPasswordAsync(string userId, SetUserPasswordRequestDto dto)
        {
            if (dto.Password != dto.PasswordConfirm)
                throw new BadRequestException("A senha e a confirmação de senha não coincidem.");

            var user = await _identityService.FindByIdAsync(userId);
            if (user == null)
                throw new NotFoundException("Usuário não encontrado.", userId);

            ThrowIfFailed(await _identityService.ResetPasswordAsync(user, dto.Password));

            await _identityService.RevokeRefreshTokensAsync(user);
        }

        private static void ThrowIfFailed(IEnumerable<IdentityError> errors)
        {
            var list = errors.ToList();
            if (list.Count > 0)
                throw new BadRequestException(string.Join(" | ", list.Select(e => e.Description)));
        }
    }
}
