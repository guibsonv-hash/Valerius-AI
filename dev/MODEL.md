# Modelo local

Modelo padrão e recomendado: `gpt-oss:20b` pelo Ollama. O seletor aceita outros modelos locais instalados. O aplicativo rejeita aliases remotos antes de enviar histórico.

O GPT OSS 20B expõe raciocínio `low`, `medium` e `high`, apresentados como Rápido, Equilibrado e Profundo. O download aproximado é 14 GB e fica visível. Nesta máquina, o teste integrado usa `qwen3:1.7b` porque não havia espaço livre suficiente para instalar o modelo padrão.

Embeddings usam `nomic-embed-text` localmente.
