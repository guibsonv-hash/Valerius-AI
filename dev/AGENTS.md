# Agentes

`WorkService` implementa o ciclo do agente da v0.2: roteamento do formato, plano persistido, produção estruturada pelo modelo, criação do arquivo, validação e registro do artifact. Cada etapa possui estado e resultado no banco.

A execução é sequencial e cancelável, adequada ao MacBook Air. Não há execução autônoma em segundo plano nem código arbitrário gerado pelo modelo.
