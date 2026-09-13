using DocumentFormat.OpenXml.Packaging;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using ValeriusAI.Core;

namespace ValeriusAI.Infrastructure;

public sealed class KnowledgeService(IWorkspaceRepository repository, IModelProvider provider) : IContextAugmenter
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".md", ".csv", ".json", ".docx", ".pptx", ".xlsx", ".pdf" };

    public async Task<KnowledgeDocument> ImportAsync(string path, AppSettings settings, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("O arquivo selecionado não foi encontrado.", path);
        if (!Supported.Contains(file.Extension)) throw new NotSupportedException("Use TXT, Markdown, CSV, JSON, DOCX, PPTX, XLSX ou PDF.");
        var id = Guid.NewGuid().ToString();
        var started = new KnowledgeDocument(id, file.Name, file.FullName, file.Extension.TrimStart('.').ToUpperInvariant(), file.Length, "Processando", DateTimeOffset.UtcNow);
        await repository.SaveDocumentAsync(started, []);
        try
        {
            var text = await Task.Run(() => Extract(path), ct);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("Nenhum texto legível foi encontrado no arquivo.");
            var pieces = Chunk(text).ToList();
            var chunks = new List<KnowledgeChunk>();
            for (var i = 0; i < pieces.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var embedding = await provider.EmbedAsync(pieces[i], settings.EmbeddingModel, ct);
                chunks.Add(new(Guid.NewGuid().ToString(), id, i, pieces[i], JsonSerializer.Serialize(embedding)));
                progress?.Report((i + 1d) / pieces.Count);
            }
            var ready = started with { Status = "Pronto" };
            await repository.SaveDocumentAsync(ready, chunks);
            return ready;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            var failed = started with { Status = "Falhou", Error = Friendly(e) };
            await repository.SaveDocumentAsync(failed, []);
            throw new InvalidOperationException(failed.Error, e);
        }
    }

    public async Task<string> ExtractForContextAsync(string path,CancellationToken ct=default)
    {
        var file=new FileInfo(path);if(!file.Exists)throw new FileNotFoundException("O arquivo selecionado não foi encontrado.",path);if(!Supported.Contains(file.Extension))throw new NotSupportedException("Use TXT, Markdown, CSV, JSON, DOCX, PPTX, XLSX ou PDF.");
        var text=await Task.Run(()=>Extract(path),ct);if(string.IsNullOrWhiteSpace(text))throw new InvalidDataException("Nenhum texto legível foi encontrado no arquivo.");return text.Length>16000?text[..16000]:text;
    }

    public async Task<string> BuildAsync(string query, AppSettings settings, CancellationToken ct = default)
    {
        var sections = new List<string>();
        if (settings.UseMemory)
        {
            var memories = await repository.GetMemoriesAsync();
            var relevant = memories.Where(x => x.Enabled).Select(x => (x.Content, ScoreWords(query, x.Content))).OrderByDescending(x => x.Item2).Where(x => x.Item2 > 0).Take(5).Select(x => $"Memória: {x.Content}");
            sections.AddRange(relevant);
        }
        if (settings.UseKnowledge)
        {
            var chunks = await repository.GetChunksAsync();
            if (chunks.Count > 0)
            {
                var queryVector = await provider.EmbedAsync(query, settings.EmbeddingModel, ct);
                var ranked = chunks.Select(x => (Chunk: x, Score: Cosine(queryVector, JsonSerializer.Deserialize<float[]>(x.EmbeddingJson) ?? []))).OrderByDescending(x => x.Score).Take(5).Where(x => x.Score > .2f);
                sections.AddRange(ranked.Select(x => $"Documento local, trecho {x.Chunk.Position + 1}: {x.Chunk.Content}"));
            }
        }
        return string.Join("\n\n", sections);
    }

    public static IEnumerable<string> Chunk(string text, int max = 1200, int overlap = 160)
    {
        var normalized = text.Replace("\r", "").Trim();
        for (var start = 0; start < normalized.Length; start += Math.Max(1, max - overlap))
        {
            var length = Math.Min(max, normalized.Length - start);
            var end = start + length;
            if (end < normalized.Length)
            {
                var breakAt = normalized.LastIndexOfAny(['\n', '.', '!', '?'], end - 1, Math.Max(1, length - 300));
                if (breakAt > start + 300) length = breakAt - start + 1;
            }
            var piece = normalized.Substring(start, length).Trim();
            if (piece.Length > 0) yield return piece;
            if (start + length >= normalized.Length) yield break;
            start = start + length - Math.Min(overlap, length) - Math.Max(1, max - overlap);
        }
    }

    private static string Extract(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".txt" or ".md" or ".csv" or ".json") return File.ReadAllText(path);
        if (ext == ".pdf")
        {
            using var pdf = PdfDocument.Open(path);
            return string.Join("\n\n", pdf.GetPages().Select(x => x.Text));
        }
        if (ext == ".docx")
        {
            using var doc = WordprocessingDocument.Open(path, false);
            var main = doc.MainDocumentPart;
            return main is null ? "" : main.Document?.Body?.InnerText ?? "";
        }
        if (ext == ".pptx")
        {
            using var doc = PresentationDocument.Open(path, false);
            var presentation = doc.PresentationPart;
            return presentation is null ? "" : string.Join("\n", presentation.SlideParts.Select(x => x.Slide?.InnerText ?? ""));
        }
        if (ext == ".xlsx")
        {
            using var doc = SpreadsheetDocument.Open(path, false);
            var workbook = doc.WorkbookPart;
            if (workbook is null) return "";
            var shared = workbook.SharedStringTablePart?.SharedStringTable?.Elements<DocumentFormat.OpenXml.Spreadsheet.SharedStringItem>().Select(x => x.InnerText).ToArray() ?? [];
            return string.Join("\n", workbook.WorksheetParts.SelectMany(x => x.Worksheet?.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>() ?? []).Select(c => c.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString && int.TryParse(c.CellValue?.Text, out var i) && i < shared.Length ? shared[i] : c.CellValue?.Text ?? ""));
        }
        throw new NotSupportedException();
    }

    private static float Cosine(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, aa = 0, bb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; aa += a[i] * a[i]; bb += b[i] * b[i]; }
        return aa == 0 || bb == 0 ? 0 : (float)(dot / Math.Sqrt(aa * bb));
    }
    private static int ScoreWords(string query, string text) => query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).Count(x => x.Length > 2 && text.Contains(x, StringComparison.OrdinalIgnoreCase));
    private static string Friendly(Exception e) => e switch
    { FileNotFoundException => "O arquivo não está mais no local selecionado.", UnauthorizedAccessException => "Sem permissão para ler o arquivo.", ModelException => e.Message, _ => "Não foi possível processar o arquivo. Confira o formato e tente novamente." };
}
