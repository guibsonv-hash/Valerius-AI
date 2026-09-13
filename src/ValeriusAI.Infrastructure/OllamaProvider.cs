using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ValeriusAI.Core;
namespace ValeriusAI.Infrastructure;

public sealed class OllamaProvider(HttpClient client) : IModelProvider
{
    public async Task<Availability> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        try { using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(3)); using var r=await client.GetAsync("api/version",timeout.Token); return new(r.IsSuccessStatusCode,r.IsSuccessStatusCode ? "Ollama conectado · inferência local" : "Ollama não respondeu. Tente iniciar o serviço."); }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { return new(false,"Ollama indisponível. Inicie o serviço ou instale o Ollama para continuar."); }
    }
    public async Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken ct = default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var r=await client.GetAsync("api/tags",timeout.Token); await Check(r);
        using var j=await JsonDocument.ParseAsync(await r.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
        return j.RootElement.GetProperty("models").EnumerateArray().Where(m=>!IsRemote(m)).Where(m=>!m.GetProperty("name").GetString()!.Contains(":cloud",StringComparison.OrdinalIgnoreCase)).Select(m=>new LocalModel(m.GetProperty("name").GetString()!,m.GetProperty("size").GetInt64())).ToList();
    }
    public async Task<ModelCapabilities> GetCapabilitiesAsync(string model,CancellationToken ct=default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response=await client.PostAsJsonAsync("api/show",new {model},timeout.Token);await Check(response);
        using var json=await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token),cancellationToken:timeout.Token);
        var families=new List<string>();
        if(json.RootElement.TryGetProperty("details",out var details) && details.TryGetProperty("families",out var familyList))
            families.AddRange(familyList.EnumerateArray().Select(x=>x.GetString()).Where(x=>x is not null)!);
        var capabilities=json.RootElement.TryGetProperty("capabilities",out var caps)
            ? caps.EnumerateArray().Select(x=>x.GetString()??"").ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        var isGptOss=model.StartsWith("gpt-oss",StringComparison.OrdinalIgnoreCase);
        return new(capabilities.Contains("tools")||isGptOss,capabilities.Contains("thinking")||isGptOss,capabilities.Contains("completion")||isGptOss,capabilities.Contains("embedding"),families,capabilities.Contains("vision"));
    }
    private static bool IsRemote(JsonElement j) => (j.TryGetProperty("remote_host",out var host) && !string.IsNullOrWhiteSpace(host.GetString())) || (j.TryGetProperty("remote_model",out var model) && !string.IsNullOrWhiteSpace(model.GetString()));
    private async Task VerifyLocalAsync(string model,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response=await client.PostAsJsonAsync("api/show",new {model},timeout.Token);await Check(response);
        using var json=await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token),cancellationToken:timeout.Token);
        if(IsRemote(json.RootElement))throw new ModelException("Este modelo aponta para um serviço remoto. Selecione um modelo local.");
    }
    private static async Task Check(HttpResponseMessage response)
    {
        if(response.IsSuccessStatusCode) return;
        await Task.CompletedTask;
        throw new ModelException(response.StatusCode == System.Net.HttpStatusCode.NotFound ? "Modelo não encontrado. Atualize a lista em Ajustes e selecione um modelo instalado." : "Ollama não conseguiu concluir a operação. Confira o serviço e tente novamente.");
    }
    private async IAsyncEnumerable<JsonElement> Lines(string endpoint, object body, [EnumeratorCancellation] CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,endpoint){Content=JsonContent.Create(body)};
        using var start=CancellationTokenSource.CreateLinkedTokenSource(ct); start.CancelAfter(TimeSpan.FromMinutes(3));
        using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,start.Token); await Check(response);
        using var stream=await response.Content.ReadAsStreamAsync(ct); using var reader=new StreamReader(stream);
        while(true)
        {
            using var idle=CancellationTokenSource.CreateLinkedTokenSource(ct); idle.CancelAfter(TimeSpan.FromMinutes(3));
            var line=await reader.ReadLineAsync(idle.Token); if(line is null) break; if(string.IsNullOrWhiteSpace(line)) continue;
            using var json=JsonDocument.Parse(line);
            if(json.RootElement.TryGetProperty("error",out _)) throw new ModelException("Ollama informou uma falha. Verifique espaço em disco, memória e disponibilidade do modelo.");
            yield return json.RootElement.Clone();
        }
    }
    public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<Message> messages, AppSettings settings, [EnumeratorCancellation] CancellationToken ct=default)
    {
        if(settings.Model.Contains("cloud",StringComparison.OrdinalIgnoreCase)) throw new ModelException("Escolha um modelo local. Modelos de nuvem estão desativados.");
        await VerifyLocalAsync(settings.Model,ct);
        var modelMessages=new List<object>{new {role="system",content=settings.SystemPrompt}};
        foreach(var message in messages)
        {
            if(message.Images is {Count:>0})
            {
                var images=new List<string>(message.Images.Count);
                foreach(var path in message.Images)images.Add(Convert.ToBase64String(await File.ReadAllBytesAsync(path,ct)));
                modelMessages.Add(new{role=message.Role,content=message.Content,images=images.ToArray()});
            }
            else modelMessages.Add(new{role=message.Role,content=message.Content,images=Array.Empty<string>()});
        }
        var done=false; var received=false;
        object think=settings.Model.StartsWith("gpt-oss",StringComparison.OrdinalIgnoreCase)?settings.ReasoningEffort:false;
        await foreach(var line in Lines("api/chat",new { model=settings.Model,messages=modelMessages,stream=true,think,keep_alive="2m",options=new {temperature=settings.Temperature,num_predict=settings.MaxTokens,num_ctx=settings.ContextSize}},ct))
        {
            if(line.TryGetProperty("message",out var msg) && msg.TryGetProperty("content",out var content) && content.GetString() is {Length:>0} token) {received=true; yield return token;}
            if(line.TryGetProperty("done",out var d) && d.GetBoolean()) done=true;
        }
        if(!done || !received) throw new ModelException("A resposta foi interrompida ou veio vazia. Tente novamente ou escolha outro modelo.");
    }
    public async Task<string> GenerateAsync(IReadOnlyList<Message> messages,AppSettings settings,CancellationToken ct=default)
    { var s=new System.Text.StringBuilder(); await foreach(var t in StreamAsync(messages,settings,ct)) s.Append(t); return s.ToString(); }
    public async Task<string> GenerateStructuredAsync(IReadOnlyList<Message> messages,AppSettings settings,string jsonSchema,CancellationToken ct=default)
    {
        await VerifyLocalAsync(settings.Model,ct);
        var modelMessages=new List<object>{new {role="system",content=settings.SystemPrompt}};
        modelMessages.AddRange(messages.Select(m=>(object)new {role=m.Role,content=m.Content}));
        using var response=await client.PostAsJsonAsync("api/chat",new {model=settings.Model,messages=modelMessages,stream=false,format=JsonDocument.Parse(jsonSchema).RootElement,options=new {temperature=settings.Temperature,num_predict=settings.MaxTokens,num_ctx=settings.ContextSize}},ct);
        await Check(response);
        using var json=await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
        var content=json.RootElement.GetProperty("message").GetProperty("content").GetString();
        if(string.IsNullOrWhiteSpace(content))throw new ModelException("O modelo não produziu a estrutura solicitada.");
        return content;
    }
    public async Task<float[]> EmbedAsync(string text,string model,CancellationToken ct=default)
    {
        using var response=await client.PostAsJsonAsync("api/embed",new {model,input=text,truncate=true,keep_alive="90s"},ct);await Check(response);
        using var json=await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
        if(!json.RootElement.TryGetProperty("embeddings",out var embeddings)||embeddings.GetArrayLength()==0)throw new ModelException("O modelo de embeddings não retornou dados.");
        return embeddings[0].EnumerateArray().Select(x=>x.GetSingle()).ToArray();
    }
    public async IAsyncEnumerable<DownloadProgress> DownloadAsync(string model,[EnumeratorCancellation] CancellationToken ct=default)
    {
        var success=false;
        await foreach(var j in Lines("api/pull",new {model,stream=true},ct))
        {
            var status=j.TryGetProperty("status",out var s)?s.GetString()??"Baixando":"Baixando";
            if(status=="success")success=true;
            yield return new(status,j.TryGetProperty("completed",out var c)?c.GetInt64():0,j.TryGetProperty("total",out var t)?t.GetInt64():0);
        }
        if(!success)throw new ModelException("O download foi interrompido antes da verificação final. Clique em baixar para retomar.");
    }
}
