using System.Runtime.CompilerServices;
using System.Text;
namespace ValeriusAI.Core;

public sealed class ChatService(IChatRepository repository, IModelProvider provider, IContextAugmenter? contextAugmenter = null)
{
    public async IAsyncEnumerable<Message> SendAsync(string conversationId, string content, AppSettings settings, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Escreva uma mensagem antes de enviar.");
        if (string.IsNullOrWhiteSpace(settings.Model)) throw new ModelException("Selecione um modelo em Ajustes.");
        settings.Validate();
        if (contextAugmenter is not null && (settings.UseMemory || settings.UseKnowledge))
        {
            var localContext = await contextAugmenter.BuildAsync(content, settings, ct);
            if (!string.IsNullOrWhiteSpace(localContext)) settings = settings with { SystemPrompt = settings.SystemPrompt + "\n\nContexto local relevante:\n" + localContext };
        }
        var user = new Message(Guid.NewGuid().ToString(), conversationId, "user", content.Trim(), DateTimeOffset.UtcNow);
        await repository.SaveMessageAsync(user);
        yield return user;
        var history = await repository.GetMessagesAsync(conversationId);
        // Budget conservador por bytes UTF-8, reservando espaço para a resposta.
        var budget = Math.Max(512, settings.ContextSize - settings.MaxTokens - Encoding.UTF8.GetByteCount(settings.SystemPrompt) - 128);
        var selected = new List<Message>();
        var used = 0;
        foreach (var m in history.Reverse().Where(m => m.State == "complete"))
        {
            var size = Encoding.UTF8.GetByteCount(m.Content) + 16;
            if (used + size > budget) break;
            selected.Insert(0, m); used += size;
        }
        if (selected.Count == 0) throw new ModelException("Esta mensagem excede o contexto configurado. Reduza o texto ou aumente o contexto em Ajustes.");
        while (selected.Count > 0 && selected[0].Role != "user") selected.RemoveAt(0);
        var assistant = new Message(Guid.NewGuid().ToString(), conversationId, "assistant", "", DateTimeOffset.UtcNow, "interrupted");
        var text = new StringBuilder();
        var lastSave = DateTime.UtcNow;
        var completed = false;
        try
        {
            await foreach (var token in provider.StreamAsync(selected, settings, ct))
            {
                text.Append(token);
                assistant = assistant with { Content = text.ToString() };
                if ((DateTime.UtcNow - lastSave).TotalSeconds >= 1)
                {
                    await repository.SaveMessageAsync(assistant);
                    lastSave = DateTime.UtcNow;
                }
                yield return assistant;
            }
            completed = true;
        }
        finally
        {
            assistant = assistant with { Content = text.ToString(), State = completed ? "complete" : "interrupted" };
            if (text.Length > 0) await repository.SaveMessageAsync(assistant);
        }
        yield return assistant;
    }
}
