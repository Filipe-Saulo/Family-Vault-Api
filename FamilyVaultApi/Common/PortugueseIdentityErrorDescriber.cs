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
    }
}
