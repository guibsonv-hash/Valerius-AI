using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ValeriusAI.Core;
using ValeriusAI.Infrastructure;
namespace ValeriusAI.App;

public partial class MessageViewModel(Message message) : ObservableObject
{
    public string Id { get; } = message.Id;
    public string Role { get; } = message.Role;
    [ObservableProperty] private string content = message.Content;
    [ObservableProperty] private string state = message.State;
    public bool IsUser => Role == "user";
}
public partial class MainViewModel(IChatRepository repository,IWorkspaceRepository workspace,IModelProvider provider,ChatService chat,ILocalRuntime runtime,KnowledgeService knowledge,WorkService work,ArtifactService artifacts,McpService mcp) : ObservableObject
{
    public ObservableCollection<Conversation> Conversations { get; } = [];
    public ObservableCollection<Conversation> VisibleConversations { get; } = [];
    public ObservableCollection<MessageViewModel> Messages { get; } = [];
    public ObservableCollection<LocalModel> Models { get; } = [];
    public ObservableCollection<Folder> Folders { get; } = [];
    public ObservableCollection<MemoryItem> Memories { get; } = [];
    public ObservableCollection<KnowledgeDocument> Documents { get; } = [];
    public ObservableCollection<SearchHit> SearchResults { get; } = [];
    public ObservableCollection<Artifact> Artifacts { get; } = [];
    public ObservableCollection<ChatAttachment> Attachments { get; } = [];
    public ObservableCollection<AgentTask> Tasks { get; } = [];
    public ObservableCollection<AgentStep> WorkSteps { get; } = [];
    public ObservableCollection<McpServer> McpServers { get; } = [];
    public ObservableCollection<ToolSetting> Tools { get; } = [];
    [ObservableProperty] private Conversation? selectedConversation;
    [ObservableProperty] private string draft = "";
    [ObservableProperty] private string status = "Conectando ao Ollama…";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool busy;
    [ObservableProperty] private bool ready;
    [ObservableProperty] private bool initialized;
    [ObservableProperty] private bool downloading;
    [ObservableProperty] private double downloadPercent;
    [ObservableProperty] private string downloadStatus = "";
    [ObservableProperty] private AppSettings settings = new();
    [ObservableProperty] private string navigation = "Chat";
    [ObservableProperty] private string pendingConversationMode = "chat";
    [ObservableProperty] private string searchQuery = "";
    [ObservableProperty] private string conversationSearch = "";
    [ObservableProperty] private string workGoal = "";
    [ObservableProperty] private string workStatus = "Descreva uma tarefa e acompanhe a execução local.";
    [ObservableProperty] private bool working;
    [ObservableProperty] private bool showingArchived;
    [ObservableProperty] private string? conversationFolderFilter;
    public bool CanEdit => Initialized && !Busy;
    public bool CanSend => Initialized && Ready && !Busy && !string.IsNullOrWhiteSpace(Draft);
    public string ConversationMode => SelectedConversation?.Mode ?? PendingConversationMode;
    public string ConversationModeLabel => ConversationMode == "create" ? "Criar" : "Chat";
    public event Action<bool>? ScrollRequested;
    public event Action? FocusRequested;
    private CancellationTokenSource? generation;
    private CancellationTokenSource? download;
    private Task? activeSend;
    partial void OnBusyChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanSend)); SendCommand.NotifyCanExecuteChanged(); NewChatCommand.NotifyCanExecuteChanged(); }
    partial void OnInitializedChanged(bool value) { OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(CanSend)); SendCommand.NotifyCanExecuteChanged(); NewChatCommand.NotifyCanExecuteChanged(); }
    partial void OnReadyChanged(bool value) { OnPropertyChanged(nameof(CanSend)); SendCommand.NotifyCanExecuteChanged(); }
    partial void OnDraftChanged(string value) { OnPropertyChanged(nameof(CanSend)); SendCommand.NotifyCanExecuteChanged(); }
    partial void OnConversationSearchChanged(string value)=>ApplyConversationFilter();
    public async Task InitializeAsync()
    {
        try { await repository.InitializeAsync(); Settings=await repository.GetSettingsAsync(); Settings.Validate(); ApplyTheme(); await ReloadAsync(); await ReloadWorkspaceAsync(); Initialized=true; if(Conversations.FirstOrDefault(x=>!x.IsArchived) is {} first) await OpenAsync(first); await RefreshAsync(); }
        catch(Exception e) { Report(e,"Não foi possível abrir o banco local. Confira espaço em disco e permissão da pasta de dados; reabra o aplicativo."); }
    }
    public async Task ReloadAsync()
    {
        var current=SelectedConversation?.Id; var all=await repository.GetConversationsAsync(); Conversations.Clear(); foreach(var c in all) Conversations.Add(c); SelectedConversation=all.FirstOrDefault(c=>c.Id==current); ApplyConversationFilter();
    }
    private void ApplyConversationFilter()
    {
        VisibleConversations.Clear();
        var query=ConversationSearch.Trim();
        foreach(var conversation in Conversations.Where(c=>c.IsArchived==ShowingArchived&&(query.Length>0||ConversationFolderFilter is null||c.FolderId==ConversationFolderFilter)&&(query.Length==0||c.Title.Contains(query,StringComparison.OrdinalIgnoreCase))).OrderByDescending(c=>c.IsPinned).ThenByDescending(c=>c.UpdatedAt)) VisibleConversations.Add(conversation);
    }
    public async Task ReloadWorkspaceAsync()
    {
        static void Replace<T>(ObservableCollection<T> target,IEnumerable<T> values){target.Clear();foreach(var value in values)target.Add(value);}
        Replace(Folders,await workspace.GetFoldersAsync());
        Replace(Memories,await workspace.GetMemoriesAsync());
        Replace(Documents,await workspace.GetDocumentsAsync());
        Replace(Artifacts,await workspace.GetArtifactsAsync());
        Replace(Tasks,await workspace.GetAgentTasksAsync());
        Replace(McpServers,await workspace.GetMcpServersAsync());
        var toolSettings=await workspace.GetToolSettingsAsync();
        if(toolSettings.Count==0)
        {
            foreach(var item in new[]{new ToolSetting("Criar arquivos",true,"Confirmar"),new ToolSetting("Ler arquivos escolhidos",true,"Confirmar"),new ToolSetting("Pesquisar memória",true,"Automático"),new ToolSetting("Pesquisar conhecimento",true,"Automático"),new ToolSetting("Data e sistema",true,"Automático")}) await workspace.SaveToolSettingAsync(item);
            toolSettings=await workspace.GetToolSettingsAsync();
        }
        Replace(Tools,toolSettings);
    }
    public async Task OpenAsync(Conversation? c)
    {
        if(Busy || c is null) return;
        try { var messages=await repository.GetMessagesAsync(c.Id); PendingConversationMode=c.Mode; SelectedConversation=c; Messages.Clear(); foreach(var m in messages) Messages.Add(new(m)); Error=""; if(Ready)Status="Ollama conectado · inferência local"; OnPropertyChanged(nameof(ConversationMode)); OnPropertyChanged(nameof(ConversationModeLabel)); ScrollRequested?.Invoke(true); }
        catch(Exception e) {Report(e,"Não foi possível abrir esta conversa.");}
    }
    [RelayCommand(CanExecute=nameof(CanEdit))]
    private void NewChat() => PrepareConversation("chat");
    public void NewCreateChat() => PrepareConversation("create");
    private void PrepareConversation(string mode)
    {
        ShowConversations(); PendingConversationMode=mode; SelectedConversation=null; Messages.Clear();Attachments.Clear(); Draft=""; Error="";
        Status=mode=="create"?"Modo Criar iniciado · descreva o arquivo que deseja produzir":"Novo chat iniciado · escreva sua mensagem";
        OnPropertyChanged(nameof(ConversationMode)); OnPropertyChanged(nameof(ConversationModeLabel)); FocusRequested?.Invoke();
    }
    [RelayCommand(CanExecute=nameof(CanSend))]
    private Task SendAsync() { activeSend=SendCoreAsync(); return activeSend; }
    private async Task SendCoreAsync()
    {
        if(!CanSend) return;
        Busy=true; Error=""; generation=new(); var input=Draft.Trim();var attachments=Attachments.ToList(); var userSaved=false; string? activeConversationId=null;
        try
        {
            var title=string.Join(" ",input.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
            var c=SelectedConversation ?? await repository.CreateAsync(title.Length>48 ? title[..48]+"…" : title,PendingConversationMode);
            activeConversationId=c.Id;SelectedConversation=c; Draft=""; Status="Preparando o modelo…";
            var lastRender=DateTime.UtcNow;
            Message? pending=null;
            if(c.Mode=="create")
            {
                var userMessage=new Message(Guid.NewGuid().ToString(),c.Id,"user",input,DateTimeOffset.UtcNow);
                await repository.SaveMessageAsync(userMessage);userSaved=true;Messages.Add(new(userMessage));ScrollRequested?.Invoke(true);
                WorkSteps.Clear();Status="Planejando a criação no seu Mac…";
                var progress=new Progress<AgentStep>(step=>{var current=WorkSteps.FirstOrDefault(x=>x.Id==step.Id);if(current is not null)WorkSteps[WorkSteps.IndexOf(current)]=step;else WorkSteps.Add(step);Status=$"Criando · {step.Description}";});
                var result=await work.ExecuteAsync(input,Settings,progress,generation.Token,c.Id);
                var response=$"Arquivo criado e validado: **{result.Artifact.Name}**\n\nSalvo em `{result.Artifact.Path}` e disponível na Biblioteca.";
                var assistantMessage=new Message(Guid.NewGuid().ToString(),c.Id,"assistant",response,DateTimeOffset.UtcNow);
                await repository.SaveMessageAsync(assistantMessage);Messages.Add(new(assistantMessage));await ReloadWorkspaceAsync();Status="Criação concluída · arquivo disponível na Biblioteca";
            }
            else
            {
                var imagePaths=attachments.Where(x=>x.Kind=="image").Select(x=>x.Path).ToList();
                if(imagePaths.Count>0&&!(await provider.GetCapabilitiesAsync(Settings.Model,generation.Token)).Vision)throw new ModelException("O modelo selecionado não aceita imagens. Escolha um modelo local com visão em Ajustes ou remova as imagens.");
                var attachmentContext=string.Join("\n\n",attachments.Where(x=>x.Kind=="document").Select(x=>$"Arquivo {x.Name}:\n{x.Context}"));
                await foreach(var m in chat.SendAsync(c.Id,input,Settings,generation.Token,attachmentContext,imagePaths))
                {
                    if(m.Role=="user") {userSaved=true; Messages.Add(new(m)); ScrollRequested?.Invoke(true); continue;}
                    pending=m;
                    if((DateTime.UtcNow-lastRender).TotalMilliseconds<65 && m.State!="complete") continue;
                    UpdateMessage(m); lastRender=DateTime.UtcNow; Status="Respondendo localmente…";
                }
                if(pending is not null) UpdateMessage(pending);
                Status="Resposta concluída · local";
            }
        }
        catch(OperationCanceledException) { Status=generation.IsCancellationRequested?"Geração interrompida":"O modelo demorou demais para responder"; if(!generation.IsCancellationRequested) Error="Sem resposta por 3 minutos. Tente novamente ou escolha um modelo menor."; }
        catch(Exception e) {Report(e,"Não foi possível gerar a resposta. Confira o Ollama e tente novamente.");}
        finally
        {
            if(!userSaved) Draft=input;else Attachments.Clear();
            try
            {
                await ReloadAsync();
                if(activeConversationId is not null)
                {
                    SelectedConversation=Conversations.FirstOrDefault(x=>x.Id==activeConversationId);
                    var saved=await repository.GetMessagesAsync(activeConversationId);Messages.Clear();foreach(var m in saved)Messages.Add(new(m));
                    OnPropertyChanged(nameof(ConversationMode));OnPropertyChanged(nameof(ConversationModeLabel));
                }
            }
            catch(Exception e) {Report(e,"Não foi possível salvar ou recarregar o histórico. Confira o espaço em disco.");}
            Busy=false; generation.Dispose(); generation=null; ScrollRequested?.Invoke(false); FocusRequested?.Invoke();
        }
    }
    private void UpdateMessage(Message m) { var vm=Messages.FirstOrDefault(x=>x.Id==m.Id); if(vm is null) Messages.Add(new(m)); else {vm.Content=m.Content;vm.State=m.State;} ScrollRequested?.Invoke(false); }
    [RelayCommand] private void Stop() {generation?.Cancel();}
    public async Task ShutdownAsync() { generation?.Cancel(); download?.Cancel(); if(activeSend is not null) await activeSend; }
    public string RuntimeInstallationUrl=>runtime.InstallationUrl;
    public async Task StartRuntimeAsync()
    {
        try {Status="Iniciando o serviço local…";await runtime.StartAsync();await RefreshAsync();}
        catch(Exception e){Report(e,"Não foi possível iniciar o serviço local. Abra Ollama e atualize os modelos.");}
    }
    public async Task RefreshAsync()
    {
        try
        {
            Error=""; var availability=await provider.CheckAvailabilityAsync(); Status=availability.Description; Ready=false; Models.Clear();
            if(!availability.Ready) return;
            foreach(var model in await provider.GetModelsAsync()) Models.Add(model);
            if(Models.Count==0) {Status="Nenhum modelo local. Abra Ajustes para baixar o recomendado.";return;}
            if(!Models.Any(m=>m.Name==Settings.Model)) {Settings=Settings with {Model=Models[0].Name};await repository.SaveSettingsAsync(Settings);}
            Ready=true;
        }
        catch(Exception e) {Report(e,"Não foi possível listar os modelos. Verifique o Ollama.");}
    }
    public async Task SaveSettingsAsync(AppSettings value)
    {
        if(Busy) return;
        value.Validate(); await repository.SaveSettingsAsync(value); Settings=value; ApplyTheme(); await RefreshAsync();
    }
    private void ApplyTheme() { if(Avalonia.Application.Current is {} app) app.RequestedThemeVariant=Settings.Theme switch {"Claro"=>Avalonia.Styling.ThemeVariant.Light,"Escuro"=>Avalonia.Styling.ThemeVariant.Dark,_=>Avalonia.Styling.ThemeVariant.Default}; }
    public async Task RenameAsync(string title) {if(SelectedConversation is {} c && !Busy){await repository.RenameAsync(c.Id,title);await ReloadAsync();}}
    public async Task DeleteAsync() {if(SelectedConversation is {} c && !Busy){await repository.DeleteAsync(c.Id);NewChat();await ReloadAsync();}}
    public async Task ClearAsync() {if(!Busy){await repository.ClearAsync();NewChat();await ReloadAsync();}}
    public async Task DownloadAsync()
    {
        if(Downloading) {download?.Cancel();return;}
        Downloading=true; download=new(); DownloadPercent=0;
        try {await foreach(var p in provider.DownloadAsync(Product.RecommendedModel,download.Token)){DownloadPercent=p.Total>0?100.0*p.Completed/p.Total:0;DownloadStatus=p.Total>0?$"{p.Completed/1e9:0.00} / {p.Total/1e9:0.00} GB · {DownloadPercent:0}%":p.Status;} DownloadPercent=100; DownloadStatus="Modelo instalado"; await RefreshAsync();}
        catch(OperationCanceledException){DownloadStatus="Download interrompido. Clique novamente para retomar.";}
        catch(Exception e){DownloadStatus="Não foi possível baixar. Confira a conexão e o espaço em disco.";Report(e,DownloadStatus);}
        finally{Downloading=false;download.Dispose();download=null;}
    }
    public void Navigate(string destination){Navigation=destination;Error="";}
    public void ShowConversations(){ShowingArchived=false;ConversationFolderFilter=null;Navigation="Chat";ApplyConversationFilter();}
    public void ShowArchived(){ShowingArchived=true;ConversationFolderFilter=null;Navigation="Chat";SelectedConversation=null;Messages.Clear();ApplyConversationFilter();}
    public void FilterByFolder(Folder folder){ShowingArchived=false;ConversationFolderFilter=folder.Id;Navigation="Chat";SelectedConversation=null;Messages.Clear();ApplyConversationFilter();}
    public async Task TogglePinAsync()
    {if(SelectedConversation is not {} c)return;await workspace.UpdateConversationAsync(c with{IsPinned=!c.IsPinned,UpdatedAt=DateTimeOffset.UtcNow});await ReloadAsync();}
    public async Task ToggleArchiveAsync()
    {if(SelectedConversation is not {} c)return;await workspace.UpdateConversationAsync(c with{IsArchived=!c.IsArchived,UpdatedAt=DateTimeOffset.UtcNow});NewChat();await ReloadAsync();}
    public async Task<Folder> CreateFolderAsync(string name)
    {var folder=new Folder(Guid.NewGuid().ToString(),name.Trim(),DateTimeOffset.UtcNow);await workspace.SaveFolderAsync(folder);await ReloadWorkspaceAsync();return folder;}
    public async Task RenameFolderAsync(Folder folder,string name)
    {if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Informe um nome para a pasta.");await workspace.SaveFolderAsync(folder with{Name=name.Trim()});await ReloadWorkspaceAsync();}
    public async Task DeleteFolderAsync(Folder folder)
    {await workspace.DeleteFolderAsync(folder.Id);if(ConversationFolderFilter==folder.Id)ConversationFolderFilter=null;await ReloadWorkspaceAsync();await ReloadAsync();}
    public async Task MoveToFolderAsync(Folder? folder)
    {if(SelectedConversation is not {} c)return;await workspace.UpdateConversationAsync(c with{FolderId=folder?.Id,UpdatedAt=DateTimeOffset.UtcNow});await ReloadAsync();}
    public async Task SaveMemoryAsync(string content)
    {var now=DateTimeOffset.UtcNow;await workspace.SaveMemoryAsync(new(Guid.NewGuid().ToString(),content.Trim(),now,now));await ReloadWorkspaceAsync();}
    public async Task DeleteMemoryAsync(string id){await workspace.DeleteMemoryAsync(id);await ReloadWorkspaceAsync();}
    public async Task ImportDocumentAsync(string path,IProgress<double>? progress=null,CancellationToken ct=default)
    {Status="Indexando conhecimento local…";await knowledge.ImportAsync(path,Settings,progress,ct);await ReloadWorkspaceAsync();Status="Documento pronto para consulta local";}
    public async Task DeleteDocumentAsync(string id){await workspace.DeleteDocumentAsync(id);await ReloadWorkspaceAsync();}
    public async Task DeleteArtifactAsync(Artifact artifact){await artifacts.DeleteAsync(artifact);await ReloadWorkspaceAsync();}
    public async Task AddLocalAttachmentsAsync(IEnumerable<string> paths)
    {
        foreach(var path in paths)
        {
            var file=new FileInfo(path);if(!file.Exists)continue;var extension=file.Extension.ToLowerInvariant();
            if(extension is ".png" or ".jpg" or ".jpeg" or ".webp")
            {if(file.Length>20_000_000)throw new InvalidDataException("Use imagens de até 20 MB.");Attachments.Add(new(Guid.NewGuid().ToString(),file.Name,file.FullName,"image"));continue;}
            var text=await knowledge.ExtractForContextAsync(file.FullName);Attachments.Add(new(Guid.NewGuid().ToString(),file.Name,file.FullName,"document",text));
        }
    }
    public async Task AddKnowledgeAttachmentAsync(KnowledgeDocument document)
    {var chunks=(await workspace.GetChunksAsync()).Where(x=>x.DocumentId==document.Id).OrderBy(x=>x.Position).Select(x=>x.Content);var text=string.Join("\n",chunks);if(string.IsNullOrWhiteSpace(text))throw new InvalidDataException("Este documento ainda não possui texto indexado.");Attachments.Add(new(Guid.NewGuid().ToString(),document.Name,document.Path,"document",text.Length>16000?text[..16000]:text));}
    public void RemoveAttachment(ChatAttachment attachment)=>Attachments.Remove(attachment);
    public async Task SearchAsync()
    {SearchResults.Clear();if(string.IsNullOrWhiteSpace(SearchQuery))return;foreach(var hit in await workspace.SearchAsync(SearchQuery))SearchResults.Add(hit);}
    public async Task OpenSearchHitAsync(SearchHit hit)
    {if(hit.Kind is "Conversa" or "Mensagem" && Conversations.FirstOrDefault(x=>x.Id==hit.Id) is {} c){Navigate("Chat");await OpenAsync(c);}}
    public async Task RunWorkAsync()
    {
        if(Working||string.IsNullOrWhiteSpace(WorkGoal))return;
        Working=true;WorkSteps.Clear();Error="";WorkStatus="Planejando no seu Mac…";generation=new();
        try
        {
            var progress=new Progress<AgentStep>(step=>{var current=WorkSteps.FirstOrDefault(x=>x.Id==step.Id);if(current is not null)WorkSteps[WorkSteps.IndexOf(current)]=step;else WorkSteps.Add(step);WorkStatus=step.Description;});
            var result=await work.ExecuteAsync(WorkGoal,Settings,progress,generation.Token);WorkStatus=$"Concluído: {result.Artifact.Name}";await ReloadWorkspaceAsync();
        }
        catch(OperationCanceledException){WorkStatus="Tarefa interrompida";}
        catch(Exception e){Report(e,"A tarefa não pôde ser concluída. Confira o modelo local e tente novamente.");WorkStatus="Falha na execução";}
        finally{generation?.Dispose();generation=null;Working=false;}
    }
    public async Task<IReadOnlyList<McpToolInfo>> TestMcpAsync(McpServer server)
    {var tools=await mcp.ListToolsAsync(server);await workspace.SaveMcpServerAsync(server with{Status=$"Conectado · {tools.Count} ferramentas"});await ReloadWorkspaceAsync();return tools;}
    public async Task SaveMcpAsync(McpServer server){await workspace.SaveMcpServerAsync(server);await ReloadWorkspaceAsync();}
    public async Task SaveToolAsync(ToolSetting tool){await workspace.SaveToolSettingAsync(tool);await ReloadWorkspaceAsync();}
    public void Report(Exception e,string fallback)
    {
        Error=e is ModelException or ArgumentException?e.Message:fallback;
        AppLog.Write(e);
    }
}
