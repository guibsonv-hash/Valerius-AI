# Valerius AI

Valerius AI 0.2.0 é um aplicativo desktop de inteligência artificial local para macOS Apple Silicon. O chat, a memória, a base de conhecimento, os arquivos criados e a inferência permanecem no computador. O funcionamento principal não exige conta, chave de API, assinatura ou serviço de IA em nuvem.

Desenvolvimento e direção de produto por [Guibson Valerio](https://guibson.com.br). Empresa desenvolvedora: [Valerius Studios](https://valeriusstudios.com).

## Executar o aplicativo

O pacote instalado nesta máquina está em:

```text
~/Applications/Valerius AI.app
```

Abra o aplicativo pelo Finder ou Spotlight. O pacote é self-contained e inclui o runtime .NET necessário. O Ollama e pelo menos um modelo local são requisitos separados.

Na primeira execução:

1. Se o Ollama estiver acessível e houver um modelo instalado, o chat abre diretamente.
2. Se o runtime estiver parado, abra Ajustes e use Iniciar Ollama.
3. Se o Ollama não estiver instalado, o aplicativo oferece um link para o instalador oficial.
4. Se nenhum modelo estiver disponível, o aplicativo explica o requisito e oferece o download recomendado quando houver espaço suficiente.

O download de modelos é sempre explícito e mostra nome, tamanho aproximado, andamento e estado. Modelos não acompanham o `.app`.

## Experiência implementada

- Chat local com streaming progressivo, cancelamento por botão ou Esc e persistência de respostas parciais.
- Composer multiline com Enter para enviar, Shift + Enter para nova linha e envio vazio bloqueado.
- Markdown com títulos, listas, citações, ênfase e blocos de código, além de seleção de texto e cópia por mensagem.
- Histórico persistido com título derivado da primeira mensagem, renomeação, exclusão, fixação e arquivamento.
- Pastas para organizar conversas. O menu de contexto por botão direito permite renomear, arquivar, excluir e mover uma conversa para uma pasta escolhida.
- Busca em conversas, mensagens, memória e documentos indexados.
- Modo Criar para produzir arquivos reais em DOCX, XLSX, PPTX, PDF, Markdown, TXT, CSV e JSON.
- Biblioteca com base de conhecimento, arquivos criados e memória explícita.
- RAG local para TXT, Markdown, CSV, JSON, DOCX, PPTX, XLSX e PDFs com camada de texto.
- Cliente MCP local por `stdio`, com cadastro explícito do comando e listagem das ferramentas oferecidas.
- Ferramentas locais registradas com estado e política de permissão.
- Temas Sistema, Claro e Escuro. A instalação nova segue a preferência do macOS.
- Interface com a identidade aprovada em `identidade-visual`, layout mínimo de 700 por 560, navegação por teclado, nomes acessíveis e tooltips nos ícones.
- Ícone único do produto aplicado à janela, ao Dock e ao bundle macOS.

## Modelo local

O provider principal é o Ollama em `http://127.0.0.1:11434`. A interface depende de `IModelProvider`, então outro backend local pode ser implementado sem acoplar a UI ao Ollama.

O modelo padrão e recomendado é `gpt-oss:20b`, da OpenAI, com download aproximado de 14 GB. Ele oferece raciocínio configurável e é adequado a máquinas com cerca de 16 GB de memória unificada, desde que haja espaço em disco. Nesta máquina, o teste integrado foi executado com `qwen3:1.7b`, pois não havia espaço livre suficiente para baixar o modelo recomendado. Embeddings usam `nomic-embed-text` localmente.

O aplicativo consulta as capacidades do modelo, rejeita aliases remotos antes de enviar conteúdo e apresenta níveis de raciocínio como Rápido, Equilibrado e Profundo quando suportados.

## Arquitetura

```text
ValeriusAI.App
  Avalonia UI, MVVM, navegação e composição por injeção de dependência
        ↓
ValeriusAI.Core
  Entidades, contratos, configurações e ChatService
        ↓
ValeriusAI.Infrastructure
  SQLite, Ollama, RAG, MCP, arquivos e execução de tarefas
```

Fluxos principais:

```text
UI → ChatService → IModelProvider → OllamaProvider
ChatService → IContextAugmenter → memória e conhecimento local
UI → WorkService → modelo estruturado → ArtifactService → Biblioteca
UI → McpService → servidor local por stdio
```

Stack atual:

- C# e .NET 10;
- Avalonia UI 11.3.22;
- CommunityToolkit.Mvvm;
- Microsoft.Extensions.DependencyInjection;
- Microsoft.Data.Sqlite;
- Markdig;
- Open XML SDK;
- PDFsharp e PdfPig;
- SDK C# oficial do Model Context Protocol.

As Views não executam SQL nem chamam o Ollama diretamente. O banco, o provider, o contexto, os artifacts e o agente possuem serviços próprios. A execução de tarefas é sequencial e cancelável para limitar o consumo no MacBook Air.

## Dados locais

No macOS:

```text
~/Library/Application Support/Valerius AI/valerius.db
~/Library/Application Support/Valerius AI/technical.log
~/Library/Application Support/Valerius AI/Arquivos/
~/.ollama/models
```

O SQLite usa WAL, chaves estrangeiras e `PRAGMA user_version`. A migração da versão 1 para a 2 preserva conversas, mensagens e configurações e adiciona pastas, memória, documentos, chunks, servidores MCP, ferramentas, tarefas, etapas e artifacts.

O banco e os arquivos não possuem criptografia própria. A proteção depende da conta macOS e do FileVault. O log técnico não registra conteúdo de chats, prompts ou respostas do modelo.

## Privacidade e segurança

O provider fala somente com loopback, sem proxy e sem redirecionamento HTTP. Não há telemetria, analytics, login, sincronização ou envio do histórico para serviços externos. Instalar o Ollama e baixar modelos usam a internet de forma explícita.

Servidores MCP executam comandos locais configurados pela pessoa. Adicione somente servidores confiáveis. A versão atual não transmite automaticamente ferramentas MCP ao modelo.

## Desenvolvimento

Requisitos:

- macOS arm64;
- .NET SDK 10.0.401 ou patch compatível;
- Ollama para o fluxo integrado com IA;
- Xcode Command Line Tools para gerar o `.icns` e aplicar a assinatura ad hoc.

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet restore
dotnet run --project src/ValeriusAI.App
```

Para inspecionar a largura mínima:

```bash
dotnet run --project src/ValeriusAI.App -- --compact
```

Atalhos de desenvolvimento:

```bash
./script/build_and_run.sh
```

## Testes

```bash
dotnet test ValeriusAI.slnx -c Release
```

A suíte cobre persistência, migração, criação e exclusão de conversas, mensagens, configurações, streaming, cancelamento, respostas parciais, erros do provider, rejeição de aliases remotos, memória, RAG, busca, pastas, fixação, arquivamento, modos Chat e Criar, geração estrutural dos oito formatos e a saída do primeiro envio do estado vazio.

O teste MCP real é opt-in porque inicia o servidor oficial de filesystem por `npx`:

```bash
VALERIUS_MCP_INTEGRATION=1 dotnet test tests/ValeriusAI.Tests/ValeriusAI.Tests.csproj -c Release --filter FullyQualifiedName~OfficialMcpClientListsFilesystemToolsWhenIntegrationIsEnabled
```

Na validação final de 12/09/2026, 29 de 29 testes passaram. O teste MCP real também passou separadamente.

## Build macOS

```bash
./scripts/package-macos.sh
```

O script publica Release self-contained para `osx-arm64`, gera o ícone macOS exclusivamente a partir de `src/ValeriusAI.App/Assets/valerius-ai-icon.png`, cria o bundle, aplica assinatura ad hoc e produz:

```text
~/Applications/Valerius AI.app
artifacts/Valerius-AI-macos-arm64.zip
```

`DOTNET_BIN` permite apontar outro executável do SDK. `APP_OUTPUT` permite escolher outro destino para o bundle. Para distribuição pública serão necessários Developer ID, hardened runtime, entitlements apropriados e notarização.

## Limitações atuais

- O build validado é `osx-arm64`. A arquitetura permanece portátil, mas o pacote Windows ainda não foi compilado nem testado.
- GPT OSS 20B não foi baixado nesta máquina por falta de espaço em disco. O teste real usa Qwen3 1.7B.
- PDFs digitalizados sem camada de texto exigem OCR externo.
- MCP aceita `stdio`; transporte HTTP e autenticação ficam para uma versão futura.
- Ferramentas MCP não são escolhidas automaticamente pelo modelo nesta versão.
- O modo Criar usa templates locais objetivos. Ele não substitui acabamento editorial ou revisão humana em documentos complexos.
- O agente executa um plano local sequencial. Não há execução autônoma em segundo plano nem código arbitrário gerado pelo modelo.
- A qualidade factual das respostas depende do modelo escolhido.

## Documentação técnica

Decisões atuais ficam em `dev/`: arquitetura, banco, modelo, memória, RAG, MCP, tools, Work, artifacts, segurança, UI e changelog. A identidade visual aprovada e suas referências ficam em `identidade-visual/`.
