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

        // Forma guardada no UserName: sem espaços nas pontas e em minúsculas
        public static string Normalizar(string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}
