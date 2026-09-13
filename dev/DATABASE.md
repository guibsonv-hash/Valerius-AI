# Banco de dados

SQLite usa WAL, chaves estrangeiras e `PRAGMA user_version`. A v0.2 migra o esquema 1 para o esquema 2, preservando conversas, mensagens e configurações existentes.

Novas tabelas: `Folder`, `Memory`, `Document`, `DocumentChunk`, `McpServer`, `ToolSetting`, `AgentTask`, `AgentStep` e `Artifact`. `Conversation` ganhou pasta, arquivamento, fixação e modo.
