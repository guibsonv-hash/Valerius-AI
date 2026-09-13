# Segurança e privacidade

O provider fala apenas com `127.0.0.1:11434`, sem proxy e sem redirecionamento. Modelos cloud e aliases remotos são recusados. Não há telemetria, conta ou sincronização.

O banco e os arquivos não possuem criptografia própria. A proteção depende da conta macOS e do FileVault. Servidores MCP executam comandos locais configurados pela pessoa e devem ser adicionados somente quando confiáveis.
