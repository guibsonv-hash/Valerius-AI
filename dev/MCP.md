# MCP

Valerius AI funciona como cliente MCP por `stdio` usando o SDK C# oficial. A tela MCP salva nome, comando, argumentos e estado de ativação, conecta ao processo configurado e lista as ferramentas expostas.

O comando é executado somente após configuração e ativação explícitas. Transporte HTTP, autenticação e passagem automática de ferramentas MCP ao modelo ficam fora desta versão.

O fluxo real foi validado com o servidor oficial `@modelcontextprotocol/server-filesystem`, iniciado por `npx`, e confirmou ferramentas de leitura e listagem.
