using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using DocumentFormat.OpenXml.Packaging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using ValeriusAI.App;
using ValeriusAI.Core;
using ValeriusAI.Infrastructure;
using Xunit;

namespace ValeriusAI.Tests;

public sealed class V2Tests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "valerius-v2-" + Guid.NewGuid());

    public V2Tests() => Directory.CreateDirectory(directory);
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private async Task<SqliteChatRepository> Repository(string name = "test.db")
    {
        var repository = new SqliteChatRepository(Path.Combine(directory, name));
        await repository.InitializeAsync();
        return repository;
    }

    [Fact]
    public async Task V1DatabaseMigratesWithoutLosingConversation()
    {
        var path = Path.Combine(directory, "migration.db");
        await using (var db = new SqliteConnection("Data Source=" + path))
        {
            await db.OpenAsync();
            var command = db.CreateCommand();
            command.CommandText = "CREATE TABLE Conversation(Id TEXT PRIMARY KEY,Title TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);CREATE TABLE Message(Id TEXT PRIMARY KEY,ConversationId TEXT NOT NULL REFERENCES Conversation(Id) ON DELETE CASCADE,Role TEXT NOT NULL,Content TEXT NOT NULL,CreatedAt TEXT NOT NULL,State TEXT NOT NULL);CREATE TABLE Setting(Key TEXT PRIMARY KEY,Value TEXT NOT NULL);INSERT INTO Conversation VALUES('c','Preservada','2026-01-01T00:00:00+00:00','2026-01-01T00:00:00+00:00');PRAGMA user_version=1;";
            await command.ExecuteNonQueryAsync();
        }
        var repository = new SqliteChatRepository(path);
        await repository.InitializeAsync();
        var item = Assert.Single(await repository.GetConversationsAsync());
        Assert.Equal("Preservada", item.Title);
        Assert.False(item.IsArchived);
        Assert.Equal("chat", item.Mode);
    }

    [Fact]
    public async Task MemoryKnowledgeAndSearchPersistLocally()
    {
        var repository = await Repository();
        var now = DateTimeOffset.UtcNow;
        await repository.SaveMemoryAsync(new("m", "Prefere respostas em português", now, now));
        var file = Path.Combine(directory, "base.txt");
        await File.WriteAllTextAsync(file, "Energia solar converte luz em eletricidade por células fotovoltaicas.");
        var service = new KnowledgeService(repository, new FakeProvider());
        var document = await service.ImportAsync(file, new());
        Assert.Equal("Pronto", document.Status);
        Assert.NotEmpty(await repository.GetChunksAsync());
        Assert.Contains(await repository.SearchAsync("fotovoltaicas"), x => x.Kind == "Conhecimento");
        Assert.Contains("Documento local", await service.BuildAsync("energia solar", new()));
    }

    [Fact]
    public async Task FolderPinAndArchivePersistTogether()
    {
        var repository=await Repository();var conversation=await repository.CreateAsync("Organizar projeto");var folder=new Folder("f","Projetos",DateTimeOffset.UtcNow);await repository.SaveFolderAsync(folder);
        await repository.UpdateConversationAsync(conversation with{FolderId=folder.Id,IsPinned=true,IsArchived=true});
        var saved=Assert.Single(await repository.GetConversationsAsync());Assert.Equal(folder.Id,saved.FolderId);Assert.True(saved.IsPinned);Assert.True(saved.IsArchived);
    }

    [Fact]
    public async Task ConversationModePersistsForChatAndCreate()
    {
        var repository=await Repository();
        var chat=await repository.CreateAsync("Conversa comum");
        var create=await repository.CreateAsync("Criar relatório","create");
        var saved=await repository.GetConversationsAsync();
        Assert.Equal("chat",saved.Single(x=>x.Id==chat.Id).Mode);
        Assert.Equal("create",saved.Single(x=>x.Id==create.Id).Mode);
        await Assert.ThrowsAsync<ArgumentException>(()=>repository.CreateAsync("Inválida","outro"));
    }

    [Fact]
    public async Task FirstSendLeavesEmptyStateAndShowsPersistedMessages()
    {
        var repository=await Repository();var provider=new FakeProvider();var chat=new ChatService(repository,provider);
        var vm=new MainViewModel(repository,repository,provider,chat,new FakeRuntime(),new KnowledgeService(repository,provider),new WorkService(repository,provider,new ArtifactService(repository,Path.Combine(directory,"output"))),new McpService());
        await vm.InitializeAsync();vm.Draft="Teste integrado";await vm.SendCommand.ExecuteAsync(null);
        Assert.NotNull(vm.SelectedConversation);Assert.Equal(2,vm.Messages.Count);Assert.Equal("Teste integrado",vm.Messages[0].Content);Assert.Equal("ok",vm.Messages[1].Content);Assert.Equal(2,(await repository.GetMessagesAsync(vm.SelectedConversation!.Id)).Count);
    }

    [Fact]
    public async Task OfficialMcpClientListsFilesystemToolsWhenIntegrationIsEnabled()
    {
        if(Environment.GetEnvironmentVariable("VALERIUS_MCP_INTEGRATION")!="1")return;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90));var service=new McpService();var server=new McpServer("m","Filesystem","stdio","npx","-y @modelcontextprotocol/server-filesystem /tmp",true,"desconectado");
        var tools=await service.ListToolsAsync(server,timeout.Token);Assert.Contains(tools,x=>x.Name.Contains("read",StringComparison.OrdinalIgnoreCase));Assert.Contains(tools,x=>x.Name.Contains("list",StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("pptx")]
    [InlineData("pdf")]
    [InlineData("md")]
    [InlineData("txt")]
    [InlineData("csv")]
    [InlineData("json")]
    public async Task ArtifactGeneratorsCreateValidatedFiles(string type)
    {
        var repository = await Repository(type + ".db");
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask("t", "Criar", "Executando", now, now);
        await repository.SaveAgentTaskAsync(task, []);
        var output = Environment.GetEnvironmentVariable("VALERIUS_ARTIFACT_OUTPUT") ?? Path.Combine(directory, "files");
        var service = new ArtifactService(repository, output);
        var content = type switch
        {
            "json" => "{\"ok\":true}",
            "xlsx" => "Mês,Receita,Despesa\nJaneiro,12500,7800\nFevereiro,13200,8100\nMarço,14100,8450",
            "pptx" => string.Join('\n', Enumerable.Range(1, 15).Select(i => $"Tópico {i}: conteúdo objetivo e legível")),
            "pdf" => string.Join('\n', Enumerable.Range(1, 70).Select(i => $"Linha {i}: relatório local validado com paginação consistente.")),
            "docx" => "Relatório Valerius AI\nObjetivo\nDocumento criado localmente.\nIndicador,Resultado\nPrivacidade,Local\nValidação,Aprovada",
            _ => "Título\nLinha 1,Valor 1\nLinha 2,Valor 2"
        };
        var artifact = await service.CreateAsync(task.Id, "Teste " + type, type, content);
        Assert.True(File.Exists(artifact.Path));
        Assert.True(artifact.Size > 0);
        if(type=="docx"){using var document=WordprocessingDocument.Open(artifact.Path,false);Assert.NotEmpty(document.MainDocumentPart!.Document!.Body!.Elements<DocumentFormat.OpenXml.Wordprocessing.Table>());}
        if(type=="xlsx"){using var document=SpreadsheetDocument.Open(artifact.Path,false);var worksheet=document.WorkbookPart!.WorksheetParts.Single();Assert.NotEmpty(worksheet.Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.CellFormula>());Assert.NotNull(worksheet.DrawingsPart);}
        if(type=="pptx"){using var document=PresentationDocument.Open(artifact.Path,false);Assert.Equal(5,document.PresentationPart!.SlideParts.Count());}
        if(type=="pdf"){using var document=PdfReader.Open(artifact.Path,PdfDocumentOpenMode.Import);Assert.True(document.PageCount>=2);}
    }

    [Fact]
    public async Task WorkServicePersistsPlanAndArtifact()
    {
        var repository = await Repository();
        var service = new WorkService(repository, new FakeProvider(), new ArtifactService(repository, Path.Combine(directory, "output")));
        var result = await service.ExecuteAsync("Crie um documento DOCX curto", new() { Model = "local" }, conversationId: "conversation-create");
        Assert.Equal("Concluída", result.Task.Status);
        Assert.Equal("conversation-create", result.Task.ConversationId);
        Assert.True(File.Exists(result.Artifact.Path));
        Assert.Equal(4, (await repository.GetAgentStepsAsync(result.Task.Id)).Count);
    }

    private sealed class FakeProvider : IModelProvider
    {
        public Task<Availability> CheckAvailabilityAsync(CancellationToken ct = default) => Task.FromResult(new Availability(true, "ok"));
        public Task<IReadOnlyList<LocalModel>> GetModelsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LocalModel>>([new("local",1)]);
        public Task<ModelCapabilities> GetCapabilitiesAsync(string model, CancellationToken ct = default) => Task.FromResult(new ModelCapabilities(true, true, true, true, []));
        public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<Message> messages, AppSettings settings, [EnumeratorCancellation] CancellationToken ct = default) { await Task.Yield(); yield return "ok"; }
        public Task<string> GenerateAsync(IReadOnlyList<Message> messages, AppSettings settings, CancellationToken ct = default) => Task.FromResult("ok");
        public Task<string> GenerateStructuredAsync(IReadOnlyList<Message> messages, AppSettings settings, string schema, CancellationToken ct = default) => Task.FromResult("{\"title\":\"Resultado\",\"content\":\"Conteúdo criado e validado localmente.\"}");
        public Task<float[]> EmbedAsync(string text, string model, CancellationToken ct = default) => Task.FromResult(text.Contains("solar", StringComparison.OrdinalIgnoreCase) ? new[] { 1f, 0f } : new[] { .8f, .2f });
        public async IAsyncEnumerable<DownloadProgress> DownloadAsync(string model, [EnumeratorCancellation] CancellationToken ct = default) { await Task.CompletedTask; yield break; }
    }
    private sealed class FakeRuntime:ILocalRuntime
    {public string? FindExecutable()=>"ollama";public string InstallationUrl=>"https://ollama.com";public Task StartAsync()=>Task.CompletedTask;}
}
