using System.Net;

namespace FamilyVaultApi.Common
{
    public static class StandardMessages
    {
        private static readonly Dictionary<HttpStatusCode, string> Messages = new()
    {
        { HttpStatusCode.OK, "Requisição bem-sucedida." },
        { HttpStatusCode.Created, "Recurso criado com sucesso." },
        { HttpStatusCode.Accepted, "Requisição aceita para processamento." },
        { HttpStatusCode.NoContent, "Requisição bem-sucedida, mas sem conteúdo de retorno." },
        { HttpStatusCode.BadRequest, "Requisição inválida." },
        { HttpStatusCode.NotFound, "Recurso não encontrado." },
        { HttpStatusCode.InternalServerError, "Erro interno do servidor." },
    };

        public static string Get(HttpStatusCode statusCode)
            => Messages.TryGetValue(statusCode, out var msg) ? msg : "Resposta padrão.";

        public static string NullBody => "O corpo da requisição não pode ser nulo.";
    }
}
