using FamilyVaultApi.Common.Validators;
using FamilyVaultApi.Data.Entities;
using FamilyVaultApi.Exceptions;
using FamilyVaultApi.Models.Dto.Requests.Account;
using FamilyVaultApi.Models.Dto.Requests.User;
using FamilyVaultApi.Models.Internal.Enums;
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
        private readonly IVerificationSendLimiter _sendLimiter;

        public PasswordService(IIdentityService identityService, IPhoneNumberService phoneNumberService, IVerificationCodeSender codeSender, IVerificationSendLimiter sendLimiter)
        {
            _identityService = identityService;
            _phoneNumberService = phoneNumberService;
            _codeSender = codeSender;
            _sendLimiter = sendLimiter;
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto dto)
        {
            var channel = ResolveChannel(dto.Phone, dto.Email);

            // Responde igual para telefone/e-mail inválido, conta inexistente ou bloqueada, para não revelar quais contas existem
            var (destination, user) = await FindAccountAsync(channel, channel == VerificationChannel.Email ? dto.Email : dto.Phone);
            if (user == null || await _identityService.IsLockedOutAsync(user))
                return;

            if (!_sendLimiter.TryAcquire(destination!))
                return;

            var code = await _identityService.GeneratePasswordResetCodeAsync(user, channel);
            await _codeSender.SendAsync(new VerificationCodeMessage(channel, destination!, code, VerificationPurpose.PasswordReset));
        }

        public async Task ResetPasswordAsync(ResetPasswordWithCodeRequestDto dto)
        {
            var channel = ResolveChannel(dto.Phone, dto.Email);

            if (dto.Password != dto.PasswordConfirm)
                throw new BadRequestException("A senha e a confirmação de senha não coincidem.");

            var (_, user) = await FindAccountAsync(channel, channel == VerificationChannel.Email ? dto.Email : dto.Phone);
            if (user == null || await _identityService.IsLockedOutAsync(user))
                throw new BadRequestException("Código inválido ou expirado.");

            if (!await _identityService.VerifyPasswordResetCodeAsync(user, dto.Code, channel))
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
                throw new NotFoundException("Usuário", userId);

            ThrowIfFailed(await _identityService.ResetPasswordAsync(user, dto.Password));

            await _identityService.RevokeRefreshTokensAsync(user);
        }

        // Exatamente um entre telefone e e-mail; o canal do código segue o dado informado
        private static VerificationChannel ResolveChannel(string? phone, string? email)
        {
            var hasPhone = !string.IsNullOrWhiteSpace(phone);
            var hasEmail = !string.IsNullOrWhiteSpace(email);
            if (hasPhone == hasEmail)
                throw new BadRequestException("Informe o telefone ou o e-mail, só um dos dois.");

            return hasEmail ? VerificationChannel.Email : VerificationChannel.Sms;
        }

        // Devolve o destino normalizado e a conta; destino inválido ou conta inexistente voltam com user null.
        // Por e-mail só vale a conta cujo UserName é esse e-mail (e-mail opcional nunca confirmado não recebe código).
        private async Task<(string? Destination, User? User)> FindAccountAsync(VerificationChannel channel, string? value)
        {
            if (channel == VerificationChannel.Email)
            {
                var email = value?.Trim() ?? string.Empty;
                if (!EmailValidator.Validar(email))
                    return (null, null);

                var normalized = EmailValidator.Normalizar(email);
                var byEmail = await _identityService.FindByNameAsync(normalized);
                // Conta criada pelo admin guarda o e-mail como foi digitado, com maiúsculas
                return (normalized, string.Equals(byEmail?.UserName, normalized, StringComparison.OrdinalIgnoreCase) ? byEmail : null);
            }

            if (!_phoneNumberService.TryValidateAndFormat(value ?? string.Empty, "BR", out var phone))
                return (null, null);

            return (phone, await _identityService.FindByPhoneAsync(phone!));
        }

        private static void ThrowIfFailed(IEnumerable<IdentityError> errors)
        {
            var list = errors.ToList();
            if (list.Count > 0)
                throw new BadRequestException(string.Join(" | ", list.Select(e => e.Description)));
        }
    }
}
