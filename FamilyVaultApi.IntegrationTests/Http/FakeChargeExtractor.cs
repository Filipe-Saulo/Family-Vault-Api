using System.Collections.Concurrent;
using System.Text;
using FamilyVaultApi.Models.Internal;
using FamilyVaultApi.Services.IService;

namespace FamilyVaultApi.IntegrationTests.Http
{
    // Extrator falso: o teste diz o que devolver (ou lançar) para os arquivos que contêm um marcador
    // de texto nos bytes. Sem regra para o marcador, responde "não é conta". Nenhuma chamada real à IA.
    public class FakeChargeExtractor : IChargeExtractor
    {
        private readonly ConcurrentDictionary<string, Func<ChargeExtractionInput, ChargeExtractionResult>> _rules = new();
        private readonly List<ChargeExtractionInput> _calls = new();
        private readonly object _lock = new();

        public void Returns(string marker, ChargeExtractionResult result) =>
            _rules[marker] = _ => result;

        public void Throws(string marker, Exception exception) =>
            _rules[marker] = _ => throw exception;

        public int CallCount
        {
            get { lock (_lock) return _calls.Count; }
        }

        public int CallsFor(string marker) => Calls(marker).Count;

        public List<ChargeExtractionInput> Calls(string marker)
        {
            lock (_lock)
                return _calls.Where(c => Contains(c.Content, marker)).ToList();
        }

        public Task<ChargeExtractionResult> ExtractAsync(ChargeExtractionInput input, CancellationToken cancellationToken)
        {
            lock (_lock) _calls.Add(input);

            foreach (var (marker, rule) in _rules)
                if (Contains(input.Content, marker))
                    return Task.FromResult(rule(input));

            return Task.FromResult(new ChargeExtractionResult { IsCharge = false });
        }

        private static bool Contains(byte[] content, string marker) =>
            Encoding.Latin1.GetString(content).Contains(marker, StringComparison.Ordinal);
    }
}
