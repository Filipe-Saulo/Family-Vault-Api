using Microsoft.AspNetCore.Identity;

namespace FamilyVaultApi.Common
{
    public class PortugueseIdentityErrorDescriber : IdentityErrorDescriber
    {
        public override IdentityError PasswordMismatch()
            => new() { Code = nameof(PasswordMismatch), Description = "Senha atual incorreta." };

        public override IdentityError InvalidToken()
            => new() { Code = nameof(InvalidToken), Description = "Código inválido ou expirado." };

        public override IdentityError PasswordTooShort(int length)
            => new() { Code = nameof(PasswordTooShort), Description = $"A senha deve ter pelo menos {length} caracteres." };

        public override IdentityError PasswordRequiresDigit()
            => new() { Code = nameof(PasswordRequiresDigit), Description = "A senha deve ter pelo menos um número." };

        public override IdentityError PasswordRequiresLower()
            => new() { Code = nameof(PasswordRequiresLower), Description = "A senha deve ter pelo menos uma letra minúscula." };

        public override IdentityError PasswordRequiresUpper()
            => new() { Code = nameof(PasswordRequiresUpper), Description = "A senha deve ter pelo menos uma letra maiúscula." };

        public override IdentityError PasswordRequiresNonAlphanumeric()
            => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "A senha deve ter pelo menos um caractere especial." };

        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars)
            => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"A senha deve ter pelo menos {uniqueChars} caracteres diferentes." };

        public override IdentityError DefaultError()
            => new() { Code = nameof(DefaultError), Description = "Ocorreu um erro desconhecido." };

        public override IdentityError ConcurrencyFailure()
            => new() { Code = nameof(ConcurrencyFailure), Description = "O registro foi alterado por outra operação. Tente novamente." };

        public override IdentityError DuplicateUserName(string userName)
            => new() { Code = nameof(DuplicateUserName), Description = $"O login '{userName}' já está em uso." };

        public override IdentityError InvalidUserName(string? userName)
            => new() { Code = nameof(InvalidUserName), Description = $"O login '{userName}' é inválido." };

        public override IdentityError DuplicateEmail(string email)
            => new() { Code = nameof(DuplicateEmail), Description = $"O e-mail '{email}' já está em uso." };

        public override IdentityError InvalidEmail(string? email)
            => new() { Code = nameof(InvalidEmail), Description = $"O e-mail '{email}' é inválido." };

        public override IdentityError DuplicateRoleName(string role)
            => new() { Code = nameof(DuplicateRoleName), Description = $"O papel '{role}' já existe." };

        public override IdentityError InvalidRoleName(string? role)
            => new() { Code = nameof(InvalidRoleName), Description = $"O papel '{role}' é inválido." };

        public override IdentityError UserAlreadyInRole(string role)
            => new() { Code = nameof(UserAlreadyInRole), Description = $"O usuário já tem o papel '{role}'." };

        public override IdentityError UserNotInRole(string role)
            => new() { Code = nameof(UserNotInRole), Description = $"O usuário não tem o papel '{role}'." };

        public override IdentityError UserAlreadyHasPassword()
            => new() { Code = nameof(UserAlreadyHasPassword), Description = "O usuário já tem senha definida." };

        public override IdentityError UserLockoutNotEnabled()
            => new() { Code = nameof(UserLockoutNotEnabled), Description = "O bloqueio não está habilitado para este usuário." };

        public override IdentityError LoginAlreadyAssociated()
            => new() { Code = nameof(LoginAlreadyAssociated), Description = "Já existe um usuário com este login externo." };

        public override IdentityError RecoveryCodeRedemptionFailed()
            => new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = "Código de recuperação inválido." };
    }
}
