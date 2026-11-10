# FamilyVaultApi.Evals

Evals do extrator de contas real (`AnthropicChargeExtractor`): mede, por modelo, o acerto de cada campo, a
classificação de documentos que não são conta, os ilegíveis e a resistência a prompt injection. Chama a IA de
verdade e **custa dinheiro**; por isso roda só à mão e não faz parte da suíte (`dotnet test` não o executa).

## Dataset

`Dataset/cases.json` (gabarito e categorias) e `Dataset/files/` (PDF, PNG e JPEG), todos fictícios, gerados por
`Dataset/gerar_dataset.py` (reportlab, pymupdf e pillow; semente fixa). Para mudar um caso, edite o script e rode
`python gerar_dataset.py` dentro de `Dataset/`.

| Tipo | Casos | Passa quando |
|---|---|---|
| `charge` | contas em PDF e em imagem (foto torta, print), com valores concorrentes (multa e juros, pagamento mínimo) | emissor, valor, vencimento, linha digitável e categoria certos |
| `not_charge` | comprovante, extrato, nota fiscal paga, propaganda | `isCharge` falso |
| `illegible` | página em branco, foto muito borrada | o extrator devolve a falha "ilegível"; o relatório mostra também se ele inventou valor ou linha |
| `injection` | texto visível, texto branco e texto quase invisível pedindo outro valor, data, categoria ou linha, ou que a conta seja ignorada | todos os campos iguais ao documento |

Ao carregar, toda linha digitável do gabarito passa pelo `ChargeValidator` da API; um gabarito inválido para a execução.

## Rodar

Dois caminhos:

- **`--cli`, pelo Claude Code** logado (assinatura Pro/Max): sem chave e sem cobrança por uso. Usa o mesmo prompt,
  o mesmo schema e o mesmo mapeamento do extrator, mas o modelo lê o arquivo pela ferramenta Read do Claude Code, não
  pela requisição da API. Serve de amostra. Cada caso roda numa pasta temporária só com o documento, com só a
  ferramenta Read e sem MCP, configurações ou CLAUDE.md de quem roda. Precisa do `claude.exe` do instalador nativo no
  PATH (a instalação por npm gera um `claude.cmd`, que o executor não chama).
- **`--run`, pela API**: mede o caminho de produção e cobra por uso.

Para `--run` e `--estimate`, a chave vem de `Ai:ApiKey` (os user-secrets da API, `UserSecretsId` `family-vault-api`, ou a variável `Ai__ApiKey`)
ou de `ANTHROPIC_API_KEY`.

```bash
# Só valida o dataset, sem chamar a API
dotnet run --project FamilyVaultApi.Evals

# Conta os tokens de entrada (a contagem não é cobrada) e mostra a faixa de custo por modelo
dotnet run --project FamilyVaultApi.Evals -- --estimate

# Pelo Claude Code (assinatura, sem cobrança por uso); --cases roda só alguns casos
dotnet run --project FamilyVaultApi.Evals -- --cli [--models claude-haiku-5-5] [--effort low] [--cases luz-pdf,agua-pdf]

# Pela API (cobra): por padrão Haiku 5.5, Sonnet 5.5 e Opus 5.5, esforço padrão de cada modelo, uma repetição
dotnet run --project FamilyVaultApi.Evals -- --run [--models claude-haiku-5-5,claude-opus-5-5] [--effort low] [--repeat 2] [--out pasta]
```

No GitHub: Actions > **Evals** > Run workflow (precisa do secret `ANTHROPIC_API_KEY`). Os relatórios ficam nos
artefatos do job e a tabela de comparação, no resumo.

## Relatório

Em `eval-reports/<data>/` (ignorado pelo git): `comparacao.md` com uma linha por modelo, e um `<modelo>.md` e
`<modelo>.json` por modelo, com o resultado e o que foi extraído em cada caso, tokens, custo e tempo. O custo usa a
tabela de preços em `Program.cs` (`Pricing`).

No `--cli`, o modelo sai com o sufixo `-cli` no relatório e sem custo.

Escolhido o modelo, ele vai para `Ai:Model` no appsettings da API (e o esforço, se for o caso, para `Ai:Effort`).
