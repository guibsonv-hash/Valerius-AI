using System.Text.Json;

namespace ValeriusAI.Core;

public static class Product
{
    public const string Name = "Valerius AI";
    public const string Id = "com.valerius.localai";
    public const string RecommendedModel = "gpt-oss:20b";
    public const string EmbeddingModel = "nomic-embed-text";
    public const long RecommendedModelBytes = 14_000_000_000;
}

public record Conversation(string Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    string? FolderId = null, bool IsArchived = false, bool IsPinned = false, string Mode = "chat");
public record Message(string Id, string ConversationId, string Role, string Content, DateTimeOffset CreatedAt, string State = "complete");
public record LocalModel(string Name, long Size) { public override string ToString() => $"{Name} · {Size / 1e9:0.0} GB"; }
public record Availability(bool Ready, string Description);
public record DownloadProgress(string Status, long Completed, long Total);
public record Folder(string Id, string Name, DateTimeOffset CreatedAt);
public record MemoryItem(string Id, string Content, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool Enabled = true);
public record KnowledgeDocument(string Id, string Name, string Path, string Type, long Size, string Status, DateTimeOffset CreatedAt, string? Error = null);
public record KnowledgeChunk(string Id, string DocumentId, int Position, string Content, string EmbeddingJson);
public record SearchHit(string Kind, string Id, string Title, string Snippet, DateTimeOffset UpdatedAt);
public record McpServer(string Id, string Name, string Transport, string Command, string Arguments, bool Enabled, string Status = "desconectado");
public record ToolSetting(string Name, bool Enabled, string Permission);
public record AgentTask(string Id, string Goal, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? ConversationId = null);
public record AgentStep(string Id, string TaskId, int Position, string Description, string Status, string? Result = null);
public record Artifact(string Id, string TaskId, string Name, string Path, string Type, long Size, DateTimeOffset CreatedAt);
public record ModelCapabilities(bool Tools, bool Reasoning, bool StructuredOutput, bool Embeddings, IReadOnlyList<string> Families);
public record ToolCall(string Name, JsonElement Arguments);

public record AppSettings
{
    public string AssistantName { get; init; } = Product.Name;
    public string Theme { get; init; } = "Sistema";
    public string Model { get; init; } = Product.RecommendedModel;
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 2048;
    public int ContextSize { get; init; } = 8192;
    public string ReasoningEffort { get; init; } = "medium";
    public bool UseMemory { get; init; } = true;
    public bool UseKnowledge { get; init; } = true;
    public string EmbeddingModel { get; init; } = Product.EmbeddingModel;
    public string SystemPrompt { get; init; } = "Você é um assistente pessoal útil, claro e cuidadoso. Responda em português do Brasil, salvo pedido em outro idioma. Admita incertezas e não invente fatos. Use Markdown quando ajudar a leitura.";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AssistantName) || AssistantName.Length > 60) throw new ArgumentException("Use um nome de assistente com até 60 caracteres.");
        if (Temperature is < 0 or > 2 || MaxTokens is < 64 or > 8192 || ContextSize is < 2048 or > 131072 || MaxTokens >= ContextSize) throw new ArgumentException("Verifique os limites: a resposta deve ser menor que o contexto.");
        if (SystemPrompt.Length > 16000) throw new ArgumentException("O prompt deve ter até 16.000 caracteres.");
        if (Theme is not ("Sistema" or "Claro" or "Escuro")) throw new ArgumentException("Escolha um tema válido.");
        if (ReasoningEffort is not ("low" or "medium" or "high")) throw new ArgumentException("Escolha um nível de raciocínio válido.");
    }
}

public interface IChatRepository
{
    Task InitializeAsync();
    Task<IReadOnlyList<Conversation>> GetConversationsAsync();
    Task<Conversation> CreateAsync(string title, string mode = "chat");
    Task RenameAsync(string id, string title);
    Task DeleteAsync(string id);
    Task ClearAsync();
    Task<IReadOnlyList<Message>> GetMessagesAsync(string conversationId);
    Task SaveMessageAsync(Message message);
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<Folder>> GetFoldersAsync();
    Task<Folder> SaveFolderAsync(Folder folder);
    Task DeleteFolderAsync(string id);
    Task UpdateConversationAsync(Conversation conversation);
    Task<IReadOnlyList<MemoryItem>> GetMemoriesAsync();
    Task SaveMemoryAsync(MemoryItem item);
    Task DeleteMemoryAsync(string id);
    Task<IReadOnlyList<KnowledgeDocument>> GetDocumentsAsync();
    Task SaveDocumentAsync(KnowledgeDocument document, IReadOnlyList<KnowledgeChunk> chunks);
    Task DeleteDocumentAsync(string id);
    Task<IReadOnlyList<KnowledgeChunk>> GetChunksAsync();
    Task<IReadOnlyList<SearchHit>> SearchAsync(string query);
    Task<IReadOnlyList<McpServer>> GetMcpServersAsync();
    Task SaveMcpServerAsync(McpServer server);
    Task DeleteMcpServerAsync(string id);
    Task<IReadOnlyList<ToolSetting>> GetToolSettingsAsync();
    Task SaveToolSettingAsync(ToolSetting setting);
    Task SaveAgentTaskAsync(AgentTask task, IReadOnlyList<AgentStep> steps);
    Task<IReadOnlyList<AgentTask>> GetAgentTasksAsync();
    Task<IReadOnlyList<AgentStep>> GetAgentStepsAsync(string taskId);
    Task SaveArtifactAsync(Artifact artifact);
    Task<IReadOnlyList<Artifact>> GetArtifactsAsync();
}

public interface IModelProvider
{
    Task<Availability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken cancellationToken = default);
    Task<ModelCapabilities> GetCapabilitiesAsync(string model, CancellationToken cancellationToken = default);
    IAsyncEnumerable<string> StreamAsync(IReadOnlyList<Message> messages, AppSettings settings, CancellationToken cancellationToken = default);
    Task<string> GenerateAsync(IReadOnlyList<Message> messages, AppSettings settings, CancellationToken cancellationToken = default);
    Task<string> GenerateStructuredAsync(IReadOnlyList<Message> messages, AppSettings settings, string jsonSchema, CancellationToken cancellationToken = default);
    Task<float[]> EmbedAsync(string text, string model, CancellationToken cancellationToken = default);
    IAsyncEnumerable<DownloadProgress> DownloadAsync(string model, CancellationToken cancellationToken = default);
}

public interface IContextAugmenter
{
    Task<string> BuildAsync(string query, AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class ModelException(string message) : Exception(message);
