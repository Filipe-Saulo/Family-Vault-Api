namespace FamilyVaultApi.Common
{
    // Mascara telefone ou e-mail para os logs: o destino completo nunca é registrado
    public static class DestinationMask
    {
        public static string Mask(string? destination)
        {
            if (string.IsNullOrEmpty(destination))
                return "****";

            var at = destination.IndexOf('@');
            if (at > 0)
                return destination[0] + "***" + destination[at..];

            if (destination.Length <= 4)
                return "****";

            return new string('*', destination.Length - 4) + destination[^4..];
        }
    }
}
