using System.Text.RegularExpressions;

namespace FamilyVaultApi.Common.Validators
{
    public static class EmailValidator
    {
        public static bool Validar(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            // Expressão regular simples para validar e-mails
            var regex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

            return regex.IsMatch(email);
        }
    }
}
