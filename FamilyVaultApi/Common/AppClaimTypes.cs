namespace FamilyVaultApi.Common
{
    public static class AppClaimTypes
    {
        public const string Permission = "permission";

        // Conta da demonstração pública: só leitura e só enxerga os usuários da demo (DemoReadOnlyMiddleware, DemoAccount)
        public const string Demo = "demo";
    }
}
