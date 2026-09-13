using System.Net;
using System.Runtime.CompilerServices;
using ValeriusAI.Core;
using ValeriusAI.Infrastructure;
using Xunit;
namespace ValeriusAI.Tests;

public sealed class ChatTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"valerius-test-"+Guid.NewGuid());
    private async Task<SqliteChatRepository> Repository()
    {Directory.CreateDirectory(directory);var r=new SqliteChatRepository(Path.Combine(directory,"test.db"));await r.InitializeAsync();return r;}
    public void Dispose(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
    [Fact] public async Task HistorySurvivesReopenAndDeleteCascades()
    {
        var r=await Repository();var c=await r.CreateAsync("Primeira conversa");await r.SaveMessageAsync(new("1",c.Id,"user","Olá, acentuação!",DateTimeOffset.UtcNow));await r.RenameAsync(c.Id,"Novo título");
        var reopened=await Repository();Assert.Equal("Novo título",Assert.Single(await reopened.GetConversationsAsync()).Title);Assert.Equal("Olá, acentuação!",Assert.Single(await reopened.GetMessagesAsync(c.Id)).Content);await reopened.DeleteAsync(c.Id);Assert.Empty(await reopened.GetMessagesAsync(c.Id));
    }
    [Fact] public async Task SettingsRoundTripAndClearPreservesSettings()
    {
        var r=await Repository();var s=new AppSettings{AssistantName="Aurora",Theme="Escuro",Model="qwen3:1.7b",SystemPrompt="Responda com atenção."};await r.SaveSettingsAsync(s);await r.CreateAsync("Teste");await r.ClearAsync();Assert.Equal(s,await (await Repository()).GetSettingsAsync());Assert.Empty(await r.GetConversationsAsync());
    }
    [Fact] public async Task StreamPersistsUserAndCompleteAssistantThroughAbstraction()
    {
        var r=await Repository();var c=await r.CreateAsync("Teste");var provider=new FakeProvider();var service=new ChatService(r,provider);var updates=new List<Message>();await foreach(var m in service.SendAsync(c.Id,"  Olá  ",new(){Model="local"}))updates.Add(m);
        Assert.True(updates.Count>=3);var saved=await r.GetMessagesAsync(c.Id);Assert.Equal(2,saved.Count);Assert.Equal("Olá",saved[0].Content);Assert.Equal("Olá, mundo!",saved[1].Content);Assert.Equal("complete",saved[1].State);Assert.Single(provider.Received!);
    }
    [Fact] public async Task InterruptedStreamSavesPartialAndDoesNotUseItAsCompleteHistory()
    {
        var r=await Repository();var c=await r.CreateAsync("Teste");var service=new ChatService(r,new FakeProvider{Fail=true});
        await Assert.ThrowsAsync<ModelException>(async()=>{await foreach(var _ in service.SendAsync(c.Id,"Oi",new(){Model="local"})){};});
        var saved=await r.GetMessagesAsync(c.Id);Assert.Equal("Olá",saved[1].Content);Assert.Equal("interrupted",saved[1].State);
    }
    [Fact] public async Task CancellationKeepsAlreadyReceivedText()
    {
        var r=await Repository();var c=await r.CreateAsync("Teste");using var ct=new CancellationTokenSource();var service=new ChatService(r,new FakeProvider());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>{await foreach(var m in service.SendAsync(c.Id,"Oi",new(){Model="local"},ct.Token)){if(m.Role=="assistant")ct.Cancel();}});
        Assert.Equal("interrupted",(await r.GetMessagesAsync(c.Id))[1].State);
    }
    [Fact] public async Task BlankInputAndMissingModelDoNotCreateMessages()
    {
        var r=await Repository();var c=await r.CreateAsync("Teste");var service=new ChatService(r,new FakeProvider());
        await Assert.ThrowsAsync<ArgumentException>(async()=>{await foreach(var _ in service.SendAsync(c.Id," ",new(){Model="local"})){};});
        await Assert.ThrowsAsync<ModelException>(async()=>{await foreach(var _ in service.SendAsync(c.Id,"Oi",new(){Model=""})){};});Assert.Empty(await r.GetMessagesAsync(c.Id));
    }
    [Fact] public async Task InvalidSettingsAreRejectedBeforeWriting()
    {var r=await Repository();await Assert.ThrowsAsync<ArgumentException>(()=>r.SaveSettingsAsync(new(){MaxTokens=8192,ContextSize=2048}));Assert.Equal(2048,(await r.GetSettingsAsync()).MaxTokens);}
    [Fact] public async Task DatabaseForeignKeysPreventOrphanMessages()
    {var r=await Repository();await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(()=>r.SaveMessageAsync(new("m","missing","user","x",DateTimeOffset.UtcNow)));}
    [Fact] public async Task ProviderParsesNdjsonAndRequiresDone()
    {
        var p=Provider("{\"message\":{\"content\":\"Olá\"},\"done\":false}\n{\"message\":{\"content\":\"!\"},\"done\":true}\n");Assert.Equal("Olá!",await p.GenerateAsync([],new(){Model="local"}));
        await Assert.ThrowsAsync<ModelException>(()=>Provider("{\"message\":{\"content\":\"partial\"}}\n").GenerateAsync([],new(){Model="local"}));
    }
    [Fact] public async Task ProviderHandlesOfflineMissingModelsAndApiErrors()
    {
        var offline=new OllamaProvider(new HttpClient(new Handler(_=>throw new HttpRequestException())){BaseAddress=new("http://127.0.0.1/")});Assert.False((await offline.CheckAvailabilityAsync()).Ready);
        Assert.Empty(await Provider("{\"models\":[]}").GetModelsAsync());
        await Assert.ThrowsAsync<ModelException>(()=>Provider("",HttpStatusCode.NotFound).GenerateAsync([],new(){Model="missing"}));
        await Assert.ThrowsAsync<ModelException>(()=>Provider("{\"error\":\"failure\"}\n").GenerateAsync([],new(){Model="local"}));
        await Assert.ThrowsAsync<ModelException>(()=>Provider("").GenerateAsync([],new(){Model="test:cloud"}));
    }
    [Fact] public async Task ProviderReportsDownloadByteProgress()
    {var p=Provider("{\"status\":\"pulling\",\"completed\":50,\"total\":100}\n{\"status\":\"success\"}\n");var updates=new List<DownloadProgress>();await foreach(var item in p.DownloadAsync("local"))updates.Add(item);Assert.Equal(50,updates[0].Completed);Assert.Equal("success",updates[1].Status);}
    [Fact] public async Task RemoteAliasesAreRejectedBeforeSendingChatContent()
    {
        var calls=new List<string>();
        var client=new HttpClient(new Handler(request=>{calls.Add(request.RequestUri!.AbsolutePath);return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"remote_host\":\"https://example.com\"}")};})){BaseAddress=new("http://127.0.0.1/")};
        await Assert.ThrowsAsync<ModelException>(()=>new OllamaProvider(client).GenerateAsync([],new(){Model="alias"}));Assert.Equal(new[]{"/api/show"},calls);
    }
    [Fact] public async Task InterruptedDownloadCannotReportSuccess()
    {await Assert.ThrowsAsync<ModelException>(async()=>{await foreach(var _ in Provider("{\"status\":\"pulling\"}\n").DownloadAsync("local")){};});}
    private static OllamaProvider Provider(string body,HttpStatusCode code=HttpStatusCode.OK)=>new(new HttpClient(new Handler(request=>new HttpResponseMessage(code){Content=new StringContent(request.RequestUri!.AbsolutePath=="/api/show"&&code==HttpStatusCode.OK?"{}":body)})){BaseAddress=new("http://127.0.0.1/")});
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> f):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(f(request));}
    private sealed class FakeProvider:IModelProvider
    {
        public bool Fail{get;init;} public IReadOnlyList<Message>? Received{get;private set;}
        public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<Message> messages,AppSettings settings,[EnumeratorCancellation]CancellationToken ct=default)
        {Received=messages;await Task.Yield();ct.ThrowIfCancellationRequested();yield return "Olá";if(Fail)throw new ModelException("Falha simulada");ct.ThrowIfCancellationRequested();yield return ", mundo!";}
        public Task<Availability> CheckAvailabilityAsync(CancellationToken ct=default)=>Task.FromResult(new Availability(true,"ok"));
        public Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<LocalModel>>([]);
        public Task<ModelCapabilities> GetCapabilitiesAsync(string model,CancellationToken ct=default)=>Task.FromResult(new ModelCapabilities(true,true,true,true,[]));
        public Task<string> GenerateAsync(IReadOnlyList<Message> messages,AppSettings settings,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task<string> GenerateStructuredAsync(IReadOnlyList<Message> messages,AppSettings settings,string jsonSchema,CancellationToken ct=default)=>Task.FromResult("{}");
        public Task<float[]> EmbedAsync(string text,string model,CancellationToken ct=default)=>Task.FromResult(new[]{1f,0f});
        public async IAsyncEnumerable<DownloadProgress> DownloadAsync(string model,[EnumeratorCancellation]CancellationToken ct=default){await Task.CompletedTask;yield break;}
    }
}
