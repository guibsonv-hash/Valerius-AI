# Arquitetura

Valerius AI v0.2 mantém três projetos. `ValeriusAI.App` contém Avalonia, MVVM e composição por injeção de dependência. `ValeriusAI.Core` contém entidades, contratos e coordenação do chat. `ValeriusAI.Infrastructure` implementa SQLite, Ollama, RAG, MCP, geração de artefatos e execução do Valerius Work.

Fluxo principal: UI → `ChatService` → `IModelProvider` → `OllamaProvider`. Contexto local: `ChatService` → `IContextAugmenter` → memória e chunks do SQLite. Tarefas: UI → `WorkService` → modelo estruturado → `ArtifactService` → validação → biblioteca.

O código é portátil em .NET e Avalonia. O pacote validado nesta versão é `osx-arm64`.
