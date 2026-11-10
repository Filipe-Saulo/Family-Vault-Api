using FamilyVaultApi.Models.Internal;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace FamilyVaultApi.Services.Service
{
    // Criptografa o refresh token do Gmail com AES-256-GCM. Formato guardado: base64(nonce 12 + texto cifrado + tag 16).
    // A chave (Gmail:TokenEncryptionKey, 32 bytes em base64) fica fora do repositório.
    public class GmailTokenProtector
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;

        private readonly byte[] _key;

        public GmailTokenProtector(IOptions<GmailOptions> options)
        {
            _key = ParseKey(options.Value.TokenEncryptionKey);
        }

        public string Protect(string plainText)
        {
            var plain = Encoding.UTF8.GetBytes(plainText);
            var output = new byte[NonceSize + plain.Length + TagSize];
            var nonce = output.AsSpan(0, NonceSize);
            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plain, output.AsSpan(NonceSize, plain.Length), output.AsSpan(NonceSize + plain.Length, TagSize));

            return Convert.ToBase64String(output);
        }

        // Chave trocada ou texto adulterado lança CryptographicException
        public string Unprotect(string protectedText)
        {
            var input = Convert.FromBase64String(protectedText);
            if (input.Length < NonceSize + TagSize)
                throw new CryptographicException("Token criptografado inválido.");

            var cipherLength = input.Length - NonceSize - TagSize;
            var plain = new byte[cipherLength];

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(input.AsSpan(0, NonceSize), input.AsSpan(NonceSize, cipherLength), input.AsSpan(NonceSize + cipherLength, TagSize), plain);

            return Encoding.UTF8.GetString(plain);
        }

        // Usado também na subida da API, para uma chave errada parar tudo em vez de falhar na primeira conexão
        public static byte[] ParseKey(string? base64Key)
        {
            byte[] key;
            try
            {
                key = Convert.FromBase64String(base64Key ?? string.Empty);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Gmail:TokenEncryptionKey deve ser uma chave de 32 bytes em base64.");
            }

            if (key.Length != 32)
                throw new InvalidOperationException("Gmail:TokenEncryptionKey deve ser uma chave de 32 bytes em base64.");

            return key;
        }
    }
}
