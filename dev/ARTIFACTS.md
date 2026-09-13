# Artifacts

Artefatos ficam em `~/Library/Application Support/Valerius AI/Arquivos`. Metadados ficam na tabela `Artifact`.

DOCX, XLSX e PPTX são criados com Open XML SDK e reabertos após a escrita. DOCX suporta hierarquia e tabelas simples. XLSX cria valores numéricos, fórmulas, filtro, cabeçalho congelado, larguras e gráfico. PPTX distribui o conteúdo em cinco slides. PDF é criado com PDFsharp, possui paginação e é reaberto em modo de importação. Formatos de texto são gravados em UTF-8 e verificados por existência e tamanho.
