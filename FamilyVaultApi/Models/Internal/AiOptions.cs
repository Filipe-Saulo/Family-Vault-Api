namespace FamilyVaultApi.Models.Internal
{
    // Seção "Ai" do appsettings. A chave (Ai:ApiKey) fica em user-secrets ou na variável Ai__ApiKey, nunca em arquivo
    // do repositório; sem ela o extrator não é registrado e os documentos ficam Pending.
    public class AiOptions
    {
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "claude-haiku-5-5";
        // low, medium, high, xhigh ou max; vazio deixa o padrão do modelo
        public string? Effort { get; set; }
        public int MaxTokens { get; set; } = 16000;
        public int TimeoutSeconds { get; set; } = 120;
        public int MaxRetries { get; set; } = 2;
    }
}
