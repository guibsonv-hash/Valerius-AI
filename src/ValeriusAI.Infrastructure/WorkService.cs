using System.Text.Json;
using ValeriusAI.Core;

namespace ValeriusAI.Infrastructure;

public sealed class WorkService(IWorkspaceRepository repository, IModelProvider provider, ArtifactService artifacts)
{
    public async Task<(AgentTask Task, Artifact Artifact)> ExecuteAsync(string goal, AppSettings settings, IProgress<AgentStep>? progress = null, CancellationToken ct = default, string? conversationId = null)
    {
        if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentException("Descreva o resultado que deseja criar.");
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid().ToString(), goal.Trim(), "Planejando", now, now, conversationId);
        var type = DetectType(goal);
        var steps = new List<AgentStep>
        {
            NewStep(task.Id, 1, "Analisar o pedido e escolher o formato"),
            NewStep(task.Id, 2, "Produzir o conteúdo com o modelo local"),
            NewStep(task.Id, 3, "Gerar e validar o arquivo"),
            NewStep(task.Id, 4, "Registrar o resultado na biblioteca")
        };
        await repository.SaveAgentTaskAsync(task, steps);
        try
        {
            Complete(steps, 0, $"Formato escolhido: {type}", progress);
            task = task with { Status = "Executando", UpdatedAt = DateTimeOffset.UtcNow }; await repository.SaveAgentTaskAsync(task, steps);
            var prompt = $"Crie o conteúdo final para este pedido: {goal}. O resultado será salvo como {type.ToUpperInvariant()}. Retorne título curto e conteúdo completo. Para planilha ou CSV, use linhas separadas e colunas por vírgula. Para JSON, retorne JSON válido no campo content.";
            var schema = "{\"type\":\"object\",\"properties\":{\"title\":{\"type\":\"string\"},\"content\":{\"type\":\"string\"}},\"required\":[\"title\",\"content\"]}";
            var response = await provider.GenerateStructuredAsync([new Message(Guid.NewGuid().ToString(), "work", "user", prompt, now)], settings with { SystemPrompt = settings.SystemPrompt + "\nVocê executa tarefas locais e entrega conteúdo completo, sem prometer trabalho futuro." }, schema, ct);
            using var json = JsonDocument.Parse(response);
            var title = json.RootElement.GetProperty("title").GetString() ?? "Resultado";
            var content = json.RootElement.GetProperty("content").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(content)) throw new InvalidDataException("O modelo retornou um documento vazio.");
            Complete(steps, 1, $"Conteúdo produzido com {content.Length} caracteres", progress);
            await repository.SaveAgentTaskAsync(task, steps);
            var artifact = await artifacts.CreateAsync(task.Id, title, type, content, ct);
            Complete(steps, 2, $"Arquivo validado: {artifact.Name}", progress);
            Complete(steps, 3, artifact.Path, progress);
            task = task with { Status = "Concluída", UpdatedAt = DateTimeOffset.UtcNow };
            await repository.SaveAgentTaskAsync(task, steps);
            return (task, artifact);
        }
        catch
        {
            task = task with { Status = ct.IsCancellationRequested ? "Interrompida" : "Falhou", UpdatedAt = DateTimeOffset.UtcNow };
            var active = steps.FirstOrDefault(x => x.Status == "Pendente"); if (active is not null) steps[steps.IndexOf(active)] = active with { Status = task.Status };
            await repository.SaveAgentTaskAsync(task, steps);
            throw;
        }
    }

    private static AgentStep NewStep(string taskId, int position, string description) => new(Guid.NewGuid().ToString(), taskId, position, description, "Pendente");
    private static void Complete(List<AgentStep> steps, int index, string result, IProgress<AgentStep>? progress)
    { steps[index] = steps[index] with { Status = "Concluída", Result = result }; progress?.Report(steps[index]); }
    private static string DetectType(string goal)
    {
        var x = goal.ToLowerInvariant();
        if (x.Contains("powerpoint") || x.Contains("apresentação") || x.Contains("slides") || x.Contains("pptx")) return "pptx";
        if (x.Contains("planilha") || x.Contains("excel") || x.Contains("xlsx")) return "xlsx";
        if (x.Contains("pdf")) return "pdf";
        if (x.Contains("word") || x.Contains("docx") || x.Contains("documento")) return "docx";
        if (x.Contains("csv")) return "csv";
        if (x.Contains("json")) return "json";
        if (x.Contains("markdown") || x.Contains(".md")) return "md";
        return "docx";
    }
}
