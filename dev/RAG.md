# RAG local

A base aceita TXT, Markdown, CSV, JSON, DOCX, PPTX, XLSX e PDF. O texto é extraído localmente, dividido em trechos sobrepostos e convertido em vetores pelo endpoint local `/api/embed` do Ollama.

Na pergunta, o vetor é comparado por similaridade de cosseno e até cinco trechos relevantes entram no prompt. Documento, chunks e embeddings permanecem no SQLite. PDFs digitalizados sem camada de texto exigem OCR externo, ainda não incluído.
