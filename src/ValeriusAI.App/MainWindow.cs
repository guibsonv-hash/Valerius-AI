using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using System.Diagnostics;
using ValeriusAI.Core;
namespace ValeriusAI.App;

public sealed class MainWindow : Window
{
    private readonly MainViewModel vm;
    private readonly TextBox composer;
    private readonly ScrollViewer scroll;
    private bool canClose;
    public MainWindow(MainViewModel viewModel)
    {
        vm=viewModel;DataContext=vm;Title=Product.Name;Icon=new WindowIcon(AssetLoader.Open(new Uri("avares://ValeriusAI/Assets/valerius-ai-icon.png")));Width=Environment.GetCommandLineArgs().Contains("--compact")?700:1120;Height=Environment.GetCommandLineArgs().Contains("--compact")?560:790;MinWidth=700;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        this.Bind(BackgroundProperty,new DynamicResourceExtension("Page"));this.Bind(ForegroundProperty,new DynamicResourceExtension("Ink"));
        var root=new Grid{ColumnDefinitions=new ColumnDefinitions("268,*")};Content=root;
        var side=new Border{Padding=new Thickness(16,22),BorderThickness=new Thickness(0,0,1,0)};side.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Sidebar"));side.Bind(Border.BorderBrushProperty,new DynamicResourceExtension("Line"));root.Children.Add(side);
        var sideGrid=new Grid{RowDefinitions=new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto")};side.Child=sideGrid;
        var brand=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,Margin=new Thickness(8,0,0,22),VerticalAlignment=VerticalAlignment.Center};
        brand.Children.Add(BrandImage(38));var brandName=Design.Text("Valerius AI",18,true);brandName.Margin=new Thickness(0,3,0,0);brandName.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("BrandTeal"));brand.Children.Add(brandName);sideGrid.Children.Add(brand);
        var add=Design.Button("＋  Novo chat","Novo chat (⌘N)");add.Classes.Add("primary");add.HorizontalAlignment=HorizontalAlignment.Stretch;add.Command=vm.NewChatCommand;Grid.SetRow(add,1);sideGrid.Children.Add(add);
        var navButtons=new Dictionary<string,Button>();var nav=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*"),ColumnSpacing=8,Margin=new Thickness(0,14,0,4)};
        var libraryNav=IconButton("M3,5 C6,4 9,5 12,7 L12,20 C9,18 6,17 3,18 Z M21,5 C18,4 15,5 12,7 L12,20 C15,18 18,17 21,18 Z","Biblioteca");libraryNav.Classes.Add("nav");libraryNav.HorizontalAlignment=HorizontalAlignment.Stretch;libraryNav.Click+=(_,_)=>vm.Navigate("Biblioteca");navButtons["Biblioteca"]=libraryNav;nav.Children.Add(libraryNav);
        var searchNav=IconButton("M10.5,4 A6.5,6.5 0 1 0 10.5,17 A6.5,6.5 0 1 0 10.5,4 M15.2,15.2 L21,21","Buscar em tudo");searchNav.Classes.Add("nav");searchNav.HorizontalAlignment=HorizontalAlignment.Stretch;searchNav.Click+=(_,_)=>vm.Navigate("Buscar");navButtons["Buscar"]=searchNav;Grid.SetColumn(searchNav,1);nav.Children.Add(searchNav);Grid.SetRow(nav,2);sideGrid.Children.Add(nav);
        var foldersPanel=new StackPanel{Spacing=4,Margin=new Thickness(0,14,0,4)};var folderHead=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto")};folderHead.Children.Add(Design.Text("PASTAS",11,true));var newFolder=Design.Button("＋","Criar pasta");newFolder.Padding=new Thickness(8,4);newFolder.MinHeight=30;newFolder.MinWidth=30;newFolder.FontSize=15;newFolder.Click+=async(_,_)=>{var name=await PromptAsync("Nova pasta","");if(!string.IsNullOrWhiteSpace(name))await vm.CreateFolderAsync(name);};Grid.SetColumn(newFolder,1);folderHead.Children.Add(newFolder);foldersPanel.Children.Add(folderHead);var folderList=new ItemsControl{ItemsSource=vm.Folders};folderList.ItemTemplate=new FuncDataTemplate<Folder>((folder,_)=>{var button=Design.Button(folder?.Name??"");button.Classes.Add("nav");button.FontSize=13;button.Padding=new Thickness(10,6);button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Click+=(_,_)=>{if(folder is not null)vm.FilterByFolder(folder);};return button;});foldersPanel.Children.Add(folderList);foldersPanel.Children.Add(Design.Text("RECENTES",11,true));Grid.SetRow(foldersPanel,3);sideGrid.Children.Add(foldersPanel);
        var list=new ListBox{ItemsSource=vm.VisibleConversations,Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
        list.ItemTemplate=new FuncDataTemplate<Conversation>((c,_)=>c is null?new Border():ConversationListItem(c));
        list.Bind(IsEnabledProperty,new Binding(nameof(vm.CanEdit)));list.Bind(ListBox.SelectedItemProperty,new Binding(nameof(vm.SelectedConversation)){Mode=BindingMode.OneWay});
        list.SelectionChanged+=async(_,_)=>{if(list.SelectedItem is Conversation c && c.Id!=vm.SelectedConversation?.Id){vm.Navigate("Chat");await vm.OpenAsync(c);}};Grid.SetRow(list,4);sideGrid.Children.Add(list);
        var footer=new StackPanel{Spacing=10,Margin=new Thickness(0,16,0,0)};
        var footerActions=new Grid{ColumnDefinitions=new ColumnDefinitions("*,*,*"),ColumnSpacing=8};
        var archived=IconButton("M4,7 L20,7 L20,20 L4,20 Z M3,3 L21,3 L21,7 L3,7 Z M9,11 L15,11","Conversas arquivadas");archived.Classes.Add("nav");archived.HorizontalAlignment=HorizontalAlignment.Stretch;archived.Click+=(_,_)=>vm.ShowArchived();footerActions.Children.Add(archived);
        var settings=IconButton("M19.43,12.98 C19.47,12.66 19.5,12.34 19.5,12 C19.5,11.66 19.47,11.34 19.42,11.02 L21.54,9.37 C21.73,9.22 21.78,8.95 21.66,8.73 L19.66,5.27 C19.54,5.05 19.27,4.96 19.05,5.05 L16.56,6.05 C16.04,5.66 15.48,5.32 14.87,5.07 L14.5,2.42 C14.47,2.18 14.25,2 14,2 L10,2 C9.75,2 9.54,2.18 9.5,2.42 L9.13,5.07 C8.52,5.32 7.96,5.66 7.44,6.05 L4.95,5.05 C4.72,4.96 4.46,5.05 4.34,5.27 L2.34,8.73 C2.21,8.95 2.27,9.22 2.46,9.37 L4.58,11.02 C4.53,11.34 4.5,11.67 4.5,12 C4.5,12.33 4.53,12.66 4.58,12.98 L2.46,14.63 C2.27,14.78 2.21,15.05 2.34,15.27 L4.34,18.73 C4.46,18.95 4.73,19.04 4.95,18.95 L7.44,17.95 C7.96,18.34 8.52,18.68 9.13,18.93 L9.5,21.58 C9.54,21.82 9.75,22 10,22 L14,22 C14.25,22 14.47,21.82 14.5,21.58 L14.87,18.93 C15.48,18.68 16.04,18.34 16.56,17.95 L19.05,18.95 C19.28,19.04 19.54,18.95 19.66,18.73 L21.66,15.27 C21.78,15.05 21.73,14.78 21.54,14.63 L19.43,12.98 M12,15.5 C10.07,15.5 8.5,13.93 8.5,12 C8.5,10.07 10.07,8.5 12,8.5 C13.93,8.5 15.5,10.07 15.5,12 C15.5,13.93 13.93,15.5 12,15.5 Z","Ajustes",true);settings.Classes.Add("nav");settings.HorizontalAlignment=HorizontalAlignment.Stretch;settings.Bind(IsEnabledProperty,new Binding(nameof(vm.CanEdit)));settings.Click+=async(_,_)=>await new SettingsWindow(vm).ShowDialog(this);Grid.SetColumn(settings,1);footerActions.Children.Add(settings);
        var about=IconButton("M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 M12,7 L12,13 M12,17 L12,17.1","Créditos e informações");about.Classes.Add("nav");about.HorizontalAlignment=HorizontalAlignment.Stretch;about.Click+=async(_,_)=>await ShowAboutAsync();Grid.SetColumn(about,2);footerActions.Children.Add(about);footer.Children.Add(footerActions);Grid.SetRow(footer,5);sideGrid.Children.Add(footer);
        var contentHost=new Grid{RowDefinitions=new RowDefinitions("68,*")};Grid.SetColumn(contentHost,1);root.Children.Add(contentHost);
        var switcher=new Border{CornerRadius=new CornerRadius(22),Padding=new Thickness(3),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};switcher.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Sidebar"));contentHost.Children.Add(switcher);
        var switchRow=new StackPanel{Orientation=Orientation.Horizontal,Spacing=2};switcher.Child=switchRow;var chatTab=Design.Button("Chat");chatTab.Classes.Add("segment");chatTab.Click+=(_,_)=>vm.NewChatCommand.Execute(null);switchRow.Children.Add(chatTab);var workTab=Design.Button("Criar");workTab.Classes.Add("segment");workTab.Click+=(_,_)=>vm.NewCreateChat();switchRow.Children.Add(workTab);
        var main=new Grid{RowDefinitions=new RowDefinitions("Auto,*,Auto,Auto"),Margin=new Thickness(30,8,30,16)};Grid.SetRow(main,1);contentHost.Children.Add(main);
        var header=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto"),Margin=new Thickness(0,0,0,14)};
        var titles=new StackPanel{Spacing=5};var titleRow=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};var title=Design.Text("Nova conversa",18,true);title.TextTrimming=TextTrimming.CharacterEllipsis;title.MaxHeight=28;titleRow.Children.Add(title);var modeText=Design.Text("Chat",11,true);var modeBadge=new Border{Child=modeText,CornerRadius=new CornerRadius(999),Padding=new Thickness(9,3),VerticalAlignment=VerticalAlignment.Center};modeBadge.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Line"));titleRow.Children.Add(modeBadge);
        var model=Design.Text("Assistente pessoal · IA local",12);model.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("Muted"));titles.Children.Add(titleRow);titles.Children.Add(model);header.Children.Add(titles);
        var more=Design.Button("•••","Opções da conversa");more.MinWidth=44;Grid.SetColumn(more,1);header.Children.Add(more);more.Click+=async(_,_)=>await ConversationMenuAsync();main.Children.Add(header);
        var chatArea=new Grid();Grid.SetRow(chatArea,1);main.Children.Add(chatArea);
        var items=new ItemsControl{ItemsSource=vm.Messages,HorizontalAlignment=HorizontalAlignment.Stretch,MaxWidth=820,Margin=new Thickness(0,0,8,0)};
        items.ItemTemplate=new FuncDataTemplate<MessageViewModel>((m,_)=>MessageCard(m!));
        scroll=new ScrollViewer{Content=items,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};chatArea.Children.Add(scroll);
        var empty=new StackPanel{Spacing=14,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Width=650,MaxWidth=760,Margin=new Thickness(16)};
        var symbol=BrandImage(72);symbol.HorizontalAlignment=HorizontalAlignment.Center;empty.Children.Add(symbol);
        var emptyModeText=Design.Text("Chat",10,true);var emptyModeBadge=new Border{Child=emptyModeText,CornerRadius=new CornerRadius(999),Padding=new Thickness(9,3),HorizontalAlignment=HorizontalAlignment.Center};emptyModeBadge.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Line"));empty.Children.Add(emptyModeBadge);
        var emptyTitle=Design.Text("O que você tem em mente hoje?",30,true);emptyTitle.HorizontalAlignment=HorizontalAlignment.Center;empty.Children.Add(emptyTitle);var emptySub=Design.Text("Converse com uma IA que funciona no seu Mac.",14);emptySub.HorizontalAlignment=HorizontalAlignment.Center;emptySub.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("Muted"));empty.Children.Add(emptySub);
        var emptyComposer=new TextBox{Watermark="Mensagem para o Valerius AI",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=58,MaxHeight=150,Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontSize=15,VerticalContentAlignment=VerticalAlignment.Center};Avalonia.Automation.AutomationProperties.SetName(emptyComposer,"Mensagem inicial");emptyComposer.Bind(TextBox.TextProperty,new Binding(nameof(vm.Draft)){Mode=BindingMode.TwoWay});emptyComposer.Bind(IsEnabledProperty,new Binding(nameof(vm.CanEdit)));
        var emptyComposerGrid=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto")};emptyComposerGrid.Children.Add(emptyComposer);var emptySend=Design.Button("↑","Enviar mensagem");emptySend.Classes.Add("primary");emptySend.MinWidth=44;emptySend.Margin=new Thickness(4);emptySend.Command=vm.SendCommand;Grid.SetColumn(emptySend,1);emptyComposerGrid.Children.Add(emptySend);var emptyComposerBorder=new Border{CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1),Padding=new Thickness(7),Child=emptyComposerGrid};emptyComposerBorder.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Surface"));emptyComposerBorder.Bind(Border.BorderBrushProperty,new DynamicResourceExtension("Line"));empty.Children.Add(emptyComposerBorder);
        emptyComposer.AddHandler(KeyDownEvent,async(_,e)=>{if(e.Key==Key.Enter&&!e.KeyModifiers.HasFlag(KeyModifiers.Shift)){e.Handled=true;if(vm.CanSend)await vm.SendCommand.ExecuteAsync(null);}},Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var example=Design.Button("Ajude-me a organizar uma ideia");example.HorizontalAlignment=HorizontalAlignment.Center;example.Click+=(_,_)=>{vm.Draft="Ajude-me a organizar uma ideia. Comece me fazendo uma pergunta.";emptyComposer.Focus();};empty.Children.Add(example);chatArea.Children.Add(empty);
        var errorBox=new Border{Padding=new Thickness(12),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,8)};errorBox.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Surface"));
        var error=Design.Text("",13);error.Bind(TextBlock.TextProperty,new Binding(nameof(vm.Error)));errorBox.Child=error;Grid.SetRow(errorBox,2);main.Children.Add(errorBox);
        var composerArea=new StackPanel{Spacing=10,Margin=new Thickness(0,16,0,0),MaxWidth=850};Grid.SetRow(composerArea,3);main.Children.Add(composerArea);
        var statusRow=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto")};var statusText=Design.Text("",12);statusText.Bind(TextBlock.TextProperty,new Binding(nameof(vm.Status)));statusText.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("Muted"));statusRow.Children.Add(statusText);
        var setup=Design.Button("Configurar");Grid.SetColumn(setup,1);setup.Click+=async(_,_)=>await new SettingsWindow(vm).ShowDialog(this);statusRow.Children.Add(setup);composerArea.Children.Add(statusRow);
        var composerBorder=new Border{CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(1),Padding=new Thickness(6)};composerBorder.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Surface"));composerBorder.Bind(Border.BorderBrushProperty,new DynamicResourceExtension("Line"));composerArea.Children.Add(composerBorder);
        var composerGrid=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto")};composerBorder.Child=composerGrid;
        composer=new TextBox{Watermark="Escreva uma mensagem…",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=54,MaxHeight=180,Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontSize=15,VerticalContentAlignment=VerticalAlignment.Center};
        Avalonia.Automation.AutomationProperties.SetName(composer,"Mensagem");composer.Bind(TextBox.TextProperty,new Binding(nameof(vm.Draft)){Mode=BindingMode.TwoWay});composer.Bind(IsEnabledProperty,new Binding(nameof(vm.CanEdit)));composerGrid.Children.Add(composer);
        composer.AddHandler(KeyDownEvent,async(_,e)=>{if(e.Key==Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift)){e.Handled=true;if(vm.CanSend)await vm.SendCommand.ExecuteAsync(null);}},Avalonia.Interactivity.RoutingStrategies.Tunnel);
        var send=Design.Button("↑","Enviar mensagem");send.Classes.Add("primary");send.MinWidth=44;send.FontSize=23;send.VerticalAlignment=VerticalAlignment.Bottom;send.Margin=new Thickness(4);send.Command=vm.SendCommand;Grid.SetColumn(send,1);composerGrid.Children.Add(send);
        var stop=Design.Button("■","Interromper geração");stop.MinWidth=44;stop.VerticalAlignment=VerticalAlignment.Bottom;stop.Margin=new Thickness(4);stop.Command=vm.StopCommand;Grid.SetColumn(stop,1);composerGrid.Children.Add(stop);
        var hint=Design.Text("Enter envia · Shift + Enter quebra a linha · A IA pode cometer erros",11);hint.HorizontalAlignment=HorizontalAlignment.Center;hint.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("Muted"));composerArea.Children.Add(hint);
        var workPage=WorkPage();var searchPage=SearchPage();var libraryPage=LibraryPage();var toolsPage=ToolsPage(false);var mcpPage=ToolsPage(true);
        foreach(var page in new Control[]{workPage,searchPage,libraryPage,toolsPage,mcpPage}){Grid.SetRow(page,1);contentHost.Children.Add(page);}
        void Update()
        {
            title.Text=vm.SelectedConversation?.Title??"Nova conversa";modeText.Text=vm.ConversationModeLabel;emptyModeText.Text=vm.ConversationModeLabel;model.Text=string.IsNullOrEmpty(vm.Settings.Model)?"Assistente pessoal · IA local":vm.Settings.Model+" · neste computador";emptyTitle.Text=vm.ConversationMode=="create"?"O que você quer criar hoje?":"O que você tem em mente hoje?";emptySub.Text=vm.ConversationMode=="create"?"Descreva o arquivo. O Valerius planeja, cria e valida tudo no seu Mac.":"Converse com uma IA que funciona no seu Mac.";
            empty.IsVisible=vm.Messages.Count==0;errorBox.IsVisible=!string.IsNullOrEmpty(vm.Error);send.IsVisible=!vm.Busy;stop.IsVisible=vm.Busy;setup.IsVisible=!vm.Ready;setup.IsEnabled=vm.CanEdit;more.IsVisible=vm.SelectedConversation is not null;
            titles.IsVisible=vm.Messages.Count>0;composerArea.IsVisible=vm.Messages.Count>0;chatTab.Classes.Set("active",vm.Navigation=="Chat"&&vm.ConversationMode=="chat");workTab.Classes.Set("active",vm.Navigation=="Chat"&&vm.ConversationMode=="create");
            main.IsVisible=vm.Navigation=="Chat";
            workPage.IsVisible=vm.Navigation=="Work";
            searchPage.IsVisible=vm.Navigation=="Buscar";
            libraryPage.IsVisible=vm.Navigation=="Biblioteca";
            toolsPage.IsVisible=vm.Navigation=="Tools";
            mcpPage.IsVisible=vm.Navigation=="MCP";
            foreach(var item in navButtons)item.Value.Classes.Set("active",item.Key==vm.Navigation);
        }
        vm.PropertyChanged+=(_,_)=>Update();vm.Messages.CollectionChanged+=(_,_)=>Update();Update();
        vm.ScrollRequested+=force=>{var follow=force||scroll.Extent.Height-scroll.Viewport.Height-scroll.Offset.Y<180; if(follow)Dispatcher.UIThread.Post(()=>scroll.ScrollToEnd(),DispatcherPriority.Background);};
        vm.FocusRequested+=()=>Dispatcher.UIThread.Post(()=>{if(vm.Messages.Count==0)emptyComposer.Focus();else composer.Focus();});
        Opened+=async(_,_)=>{await vm.InitializeAsync();if(vm.Messages.Count==0)emptyComposer.Focus();else composer.Focus();};
        Closing+=async(_,e)=>{if(canClose)return;e.Cancel=true;await vm.ShutdownAsync();canClose=true;Close();};
        KeyDown+=(_,e)=>{if((e.KeyModifiers.HasFlag(KeyModifiers.Meta)||e.KeyModifiers.HasFlag(KeyModifiers.Control))&&e.Key==Key.N&&vm.CanEdit){vm.NewChatCommand.Execute(null);e.Handled=true;}if(e.Key==Key.Escape&&vm.Busy)vm.StopCommand.Execute(null);};
        SizeChanged+=(_,_)=>{var sidebarWidth=Bounds.Width<900?224:268;root.ColumnDefinitions[0].Width=new GridLength(sidebarWidth);main.Margin=new Thickness(Bounds.Width<900?18:30,8,Bounds.Width<900?18:30,16);empty.Width=Math.Max(360,Math.Min(650,Bounds.Width-sidebarWidth-90));};
    }
    private static Image BrandImage(double size)=>new(){Source=new Bitmap(AssetLoader.Open(new Uri("avares://ValeriusAI/Assets/valerius-ai-icon.png"))),Width=size,Height=size,Stretch=Stretch.Uniform};
    private static Button IconButton(string geometry,string label,bool filled=false)
    {
        var icon=new Avalonia.Controls.Shapes.Path{Data=Geometry.Parse(geometry),Width=18,Height=18,Stretch=Stretch.Uniform,StrokeThickness=1.6,StrokeLineCap=PenLineCap.Round,StrokeJoin=PenLineJoin.Round};
        icon.Bind(filled?Avalonia.Controls.Shapes.Shape.FillProperty:Avalonia.Controls.Shapes.Shape.StrokeProperty,new DynamicResourceExtension("Ink"));
        var button=new Button{Content=icon,MinHeight=38,Padding=new Thickness(9),HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};Avalonia.Automation.AutomationProperties.SetName(button,label);ToolTip.SetTip(button,label);return button;
    }
    private Control ConversationListItem(Conversation conversation)
    {
        var panel=new StackPanel{Margin=new Thickness(2,4)};var title=Design.Text((conversation.IsPinned?"Fixada  ·  ":"")+conversation.Title,14);title.TextTrimming=TextTrimming.CharacterEllipsis;title.TextWrapping=TextWrapping.NoWrap;panel.Children.Add(title);
        var host=new Border{Child=panel,Background=Brushes.Transparent,HorizontalAlignment=HorizontalAlignment.Stretch};Avalonia.Automation.AutomationProperties.SetName(host,conversation.Title);
        var rename=new MenuItem{Header="Renomear"};rename.Click+=async(_,_)=>await PerformConversationActionAsync(conversation,"Renomear");
        var move=new MenuItem{Header="Mover para pasta"};var folderItems=new List<MenuItem>();var noFolder=new MenuItem{Header="Sem pasta"};noFolder.Click+=async(_,_)=>{await vm.OpenAsync(conversation);await Safe(()=>vm.MoveToFolderAsync(null));};folderItems.Add(noFolder);foreach(var folder in vm.Folders){var item=new MenuItem{Header=folder.Name};item.Click+=async(_,_)=>{await vm.OpenAsync(conversation);await Safe(()=>vm.MoveToFolderAsync(folder));};folderItems.Add(item);}move.ItemsSource=folderItems;
        var archive=new MenuItem{Header=conversation.IsArchived?"Restaurar":"Arquivar"};archive.Click+=async(_,_)=>await PerformConversationActionAsync(conversation,conversation.IsArchived?"Restaurar":"Arquivar");
        var remove=new MenuItem{Header="Excluir"};remove.Click+=async(_,_)=>await PerformConversationActionAsync(conversation,"Excluir");
        var menu=new ContextMenu{ItemsSource=new[]{rename,move,archive,remove}};host.ContextMenu=menu;
        host.AddHandler(PointerPressedEvent,(_,e)=>{if(e.GetCurrentPoint(host).Properties.PointerUpdateKind==PointerUpdateKind.RightButtonPressed){e.Handled=true;menu.Open(host);}},Avalonia.Interactivity.RoutingStrategies.Tunnel);return host;
    }
    private Border PageShell(string title,string subtitle,Control content)
    {
        var panel=new StackPanel{Spacing=10,Margin=new Thickness(34,28)};panel.Children.Add(Design.Text(title,28,true));var sub=Design.Text(subtitle,14);sub.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("Muted"));panel.Children.Add(sub);panel.Children.Add(new Separator{Margin=new Thickness(0,8)});panel.Children.Add(content);
        var shell=new Border{Child=new ScrollViewer{Content=panel,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled}};shell.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Page"));return shell;
    }
    private Border WorkPage()
    {
        var body=new StackPanel{Spacing=14,MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Left};
        var goal=new TextBox{Watermark="Ex.: crie uma apresentação sobre energia solar",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=100,MaxHeight=190,VerticalContentAlignment=VerticalAlignment.Top};goal.Bind(TextBox.TextProperty,new Binding(nameof(vm.WorkGoal)){Mode=BindingMode.TwoWay});body.Children.Add(goal);
        var run=Design.Button("Executar tarefa local");run.Classes.Add("primary");run.Click+=async(_,_)=>await vm.RunWorkAsync();body.Children.Add(run);
        var status=Design.Text(vm.WorkStatus,13,true);status.Bind(TextBlock.TextProperty,new Binding(nameof(vm.WorkStatus)));body.Children.Add(status);
        body.Children.Add(Design.Text("PLANO E PROGRESSO",11,true));var steps=new ItemsControl{ItemsSource=vm.WorkSteps};steps.ItemTemplate=new FuncDataTemplate<AgentStep>((x,_)=>Design.Text($"{x?.Position}. {x?.Description} · {x?.Status}",14));body.Children.Add(steps);
        body.Children.Add(Design.Text("A criação do arquivo começa somente ao clicar em executar. Você pode interromper com Esc.",12));
        return PageShell("Criar um arquivo","Descreva o resultado. O Valerius planeja a tarefa, cria o arquivo de verdade e guarda uma cópia na Biblioteca.",body);
    }
    private Grid LibraryPage()
    {
        var grid=new Grid{RowDefinitions=new RowDefinitions("Auto,*")};grid.Bind(BackgroundProperty,new DynamicResourceExtension("Page"));
        var tabs=new StackPanel{Orientation=Orientation.Horizontal,Spacing=2};var tabChrome=new Border{Child=tabs,CornerRadius=new CornerRadius(22),Padding=new Thickness(3),Margin=new Thickness(34,18,34,8),HorizontalAlignment=HorizontalAlignment.Left};tabChrome.Bind(Border.BackgroundProperty,new DynamicResourceExtension("Sidebar"));grid.Children.Add(tabChrome);var tabButtons=new Dictionary<string,Button>();
        var knowledge=KnowledgePage();var files=FilesPage();var memory=MemoryPage();
        foreach(var page in new Control[]{knowledge,files,memory}){Grid.SetRow(page,1);grid.Children.Add(page);}
        void Show(string section){knowledge.IsVisible=section=="Conhecimento";files.IsVisible=section=="Arquivos";memory.IsVisible=section=="Memória";foreach(var item in tabButtons)item.Value.Classes.Set("active",item.Key==section);}
        foreach(var section in new[]{"Conhecimento","Arquivos criados","Memória"}){var key=section.StartsWith("Arquivos",StringComparison.Ordinal)?"Arquivos":section;var button=Design.Button(section);button.Classes.Add("segment");button.Click+=(_,_)=>Show(key);tabButtons[key]=button;tabs.Children.Add(button);}Show("Conhecimento");return grid;
    }
    private Border SearchPage()
    {
        var body=new StackPanel{Spacing=12,MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Stretch};var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto")};var query=new TextBox{Watermark="Buscar em conversas, mensagens, memória e conhecimento"};query.Bind(TextBox.TextProperty,new Binding(nameof(vm.SearchQuery)){Mode=BindingMode.TwoWay});row.Children.Add(query);var search=Design.Button("Buscar");search.Classes.Add("primary");search.Click+=async(_,_)=>await vm.SearchAsync();Grid.SetColumn(search,1);row.Children.Add(search);body.Children.Add(row);
        var results=new ItemsControl{ItemsSource=vm.SearchResults};results.ItemTemplate=new FuncDataTemplate<SearchHit>((x,_)=>{var b=Design.Button($"{x?.Kind}  ·  {x?.Title}\n{x?.Snippet}");b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Click+=async(_,_)=>{if(x is not null)await vm.OpenSearchHitAsync(x);};return b;});body.Children.Add(results);return PageShell("Busca global","Encontre rapidamente o que já foi conversado, lembrado ou indexado.",body);
    }
    private Border KnowledgePage()
    {
        var body=new StackPanel{Spacing=14,MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Stretch};var add=Design.Button("Adicionar documentos…");add.Classes.Add("primary");add.Click+=async(_,_)=>
        {
            try{var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{Title="Adicionar à base de conhecimento",AllowMultiple=true,FileTypeFilter=[new FilePickerFileType("Documentos suportados"){Patterns=["*.txt","*.md","*.csv","*.json","*.docx","*.pptx","*.xlsx","*.pdf"]}]});foreach(var file in files)await vm.ImportDocumentAsync(file.Path.LocalPath);}catch(Exception e){vm.Report(e,"Não foi possível indexar o documento selecionado.");}
        };body.Children.Add(add);body.Children.Add(Design.Text("O texto é extraído e os embeddings são gerados pelo Ollama neste computador.",12));
        var list=new ItemsControl{ItemsSource=vm.Documents};list.ItemTemplate=new FuncDataTemplate<KnowledgeDocument>((x,_)=>{var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto"),Margin=new Thickness(0,5)};row.Children.Add(Design.Text($"{x?.Name}\n{x?.Status} · {x?.Size/1e6:0.0} MB",14));var remove=Design.Button("Remover");remove.Click+=async(_,_)=>{if(x is not null)await vm.DeleteDocumentAsync(x.Id);};Grid.SetColumn(remove,1);row.Children.Add(remove);return row;});body.Children.Add(list);return PageShell("Conhecimento","Documentos locais disponíveis para recuperação semântica no chat.",body);
    }
    private Border FilesPage()
    {
        var body=new StackPanel{Spacing=12,MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Stretch};var list=new ItemsControl{ItemsSource=vm.Artifacts};list.ItemTemplate=new FuncDataTemplate<Artifact>((x,_)=>{var button=Design.Button($"{x?.Name}  ·  {x?.Type}  ·  {x?.Size/1024.0:0} KB");button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Click+=(_,_)=>{if(x is not null)Process.Start(new ProcessStartInfo(x.Path){UseShellExecute=true});};return button;});body.Children.Add(list);return PageShell("Arquivos","Resultados reais criados pelo Valerius Work, validados e guardados localmente.",body);
    }
    private Border MemoryPage()
    {
        var body=new StackPanel{Spacing=12,MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Stretch};var add=Design.Button("Adicionar memória…");add.Classes.Add("primary");add.Click+=async(_,_)=>{var text=await PromptAsync("Nova memória","");if(!string.IsNullOrWhiteSpace(text))await vm.SaveMemoryAsync(text);};body.Children.Add(add);body.Children.Add(Design.Text("Memórias são explícitas, editáveis e podem ser removidas a qualquer momento.",12));
        var list=new ItemsControl{ItemsSource=vm.Memories};list.ItemTemplate=new FuncDataTemplate<MemoryItem>((x,_)=>{var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto"),Margin=new Thickness(0,5)};row.Children.Add(Design.Text(x?.Content??"",14));var remove=Design.Button("Remover");remove.Click+=async(_,_)=>{if(x is not null)await vm.DeleteMemoryAsync(x.Id);};Grid.SetColumn(remove,1);row.Children.Add(remove);return row;});body.Children.Add(list);return PageShell("Memória","Preferências e fatos que você decidiu manter disponíveis para o assistente.",body);
    }
    private Border ToolsPage(bool mcp)
    {
        var body=new StackPanel{Spacing=12,MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Stretch};
        if(!mcp){body.Children.Add(Design.Text("Ferramentas nativas",18,true));var list=new ItemsControl{ItemsSource=vm.Tools};list.ItemTemplate=new FuncDataTemplate<ToolSetting>((x,_)=>Design.Text($"{(x?.Enabled==true?"Ativa":"Inativa")}  ·  {x?.Name}  ·  {x?.Permission}",14));body.Children.Add(list);body.Children.Add(Design.Text("Ações que leem arquivos ou gravam resultados exigem uma escolha explícita na interface.",12));return PageShell("Tools","Capacidades locais usadas pelo chat e pelo modo Work.",body);}
        var add=Design.Button("Adicionar servidor stdio…");add.Classes.Add("primary");add.Click+=async(_,_)=>await AddMcpAsync();body.Children.Add(add);var servers=new ItemsControl{ItemsSource=vm.McpServers};servers.ItemTemplate=new FuncDataTemplate<McpServer>((x,_)=>{var button=Design.Button($"{x?.Name}  ·  {x?.Status}");button.Click+=async(_,_)=>{if(x is null)return;try{var found=await vm.TestMcpAsync(x);button.Content=$"{x.Name} · {found.Count} ferramentas";}catch(Exception e){vm.Report(e,"Não foi possível conectar ao servidor MCP.");}};return button;});body.Children.Add(servers);body.Children.Add(Design.Text("Comandos MCP são iniciados localmente por stdio. Revise o comando antes de ativar o servidor.",12));return PageShell("MCP","Conecte servidores compatíveis pelo SDK oficial para ampliar ferramentas locais.",body);
    }
    private async Task AddMcpAsync()
    {
        var dialog=Dialog("Adicionar servidor MCP");var panel=new StackPanel{Margin=new Thickness(24),Spacing=12};var name=new TextBox{Watermark="Nome"};var command=new TextBox{Watermark="Comando, por exemplo npx"};var args=new TextBox{Watermark="Argumentos"};var enabled=new CheckBox{Content="Ativar servidor",IsChecked=true};panel.Children.Add(Design.Text("Servidor MCP local",22,true));panel.Children.Add(name);panel.Children.Add(command);panel.Children.Add(args);panel.Children.Add(enabled);var save=Design.Button("Salvar servidor");save.Classes.Add("primary");save.Click+=(_,_)=>dialog.Close(true);panel.Children.Add(save);dialog.Content=panel;if(await dialog.ShowDialog<bool>(this)&&!string.IsNullOrWhiteSpace(name.Text)&&!string.IsNullOrWhiteSpace(command.Text))await vm.SaveMcpAsync(new McpServer(Guid.NewGuid().ToString(),name.Text.Trim(),"stdio",command.Text.Trim(),args.Text?.Trim()??"",enabled.IsChecked==true,"desconectado"));
    }
    private async Task ConversationMenuAsync()
    {
        if(vm.SelectedConversation is not {} conversation)return;
        var dialog=Dialog("Opções da conversa");var panel=new StackPanel{Margin=new Thickness(24),Spacing=8};panel.Children.Add(Design.Text("Opções da conversa",21,true));
        foreach(var option in new[]{"Renomear","Mover para uma pasta",conversation.IsPinned?"Desafixar":"Fixar",conversation.IsArchived?"Restaurar":"Arquivar","Excluir"}){var button=Design.Button(option);button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Click+=(_,_)=>dialog.Close(option);panel.Children.Add(button);}dialog.Content=panel;var action=await dialog.ShowDialog<string?>(this);
        if(action is not null)await PerformConversationActionAsync(conversation,action);
    }
    private async Task PerformConversationActionAsync(Conversation conversation,string action)
    {
        if(vm.SelectedConversation?.Id!=conversation.Id)await vm.OpenAsync(conversation);
        if(action=="Renomear"){var title=await PromptAsync("Renomear conversa",conversation.Title);if(title is not null)await Safe(()=>vm.RenameAsync(title));}
        else if(action=="Mover para uma pasta")await MoveConversationAsync();
        else if(action is "Fixar" or "Desafixar")await Safe(vm.TogglePinAsync);
        else if(action is "Arquivar" or "Restaurar")await Safe(vm.ToggleArchiveAsync);
        else if(action=="Excluir"&&await ConfirmAsync("Excluir esta conversa?","As mensagens serão removidas deste computador.","Excluir"))await Safe(vm.DeleteAsync);
    }
    private async Task ShowAboutAsync()
    {
        var dialog=Dialog("Créditos e informações");dialog.Width=530;dialog.Height=650;dialog.SizeToContent=SizeToContent.Manual;var panel=new StackPanel{Margin=new Thickness(28),Spacing=10};var logo=BrandImage(58);logo.HorizontalAlignment=HorizontalAlignment.Center;panel.Children.Add(logo);var product=Design.Text("Valerius AI",24,true);product.HorizontalAlignment=HorizontalAlignment.Center;panel.Children.Add(product);var local=Design.Text("LOCAL POR NATUREZA",11,true);local.HorizontalAlignment=HorizontalAlignment.Center;local.Bind(TextBlock.ForegroundProperty,new DynamicResourceExtension("BrandTeal"));panel.Children.Add(local);
        var privacy=Design.Text("Conversas, memória, documentos e inferências permanecem neste computador quando um modelo local é utilizado.",13);privacy.TextAlignment=TextAlignment.Center;panel.Children.Add(privacy);panel.Children.Add(new Separator{Margin=new Thickness(0,6)});
        panel.Children.Add(Design.Text("CRÉDITOS",11,true));panel.Children.Add(Design.Text("Desenvolvimento e direção de produto",12));panel.Children.Add(Design.Text("Guibson Valerio",17,true));var developerSite=Design.Button("guibson.com.br  ↗");developerSite.HorizontalAlignment=HorizontalAlignment.Stretch;developerSite.HorizontalContentAlignment=HorizontalAlignment.Left;developerSite.Click+=(_,_)=>OpenUrl("https://guibson.com.br");panel.Children.Add(developerSite);
        panel.Children.Add(Design.Text("Empresa desenvolvedora",12));panel.Children.Add(Design.Text("Valerius Studios",17,true));var studioSite=Design.Button("valeriusstudios.com  ↗");studioSite.HorizontalAlignment=HorizontalAlignment.Stretch;studioSite.HorizontalContentAlignment=HorizontalAlignment.Left;studioSite.Click+=(_,_)=>OpenUrl("https://valeriusstudios.com");panel.Children.Add(studioSite);panel.Children.Add(Design.Text("Código-fonte: github.com/guibsonv-hash/Valerius-AI",13));panel.Children.Add(Design.Text("Versão 0.2.0",13));
        panel.Children.Add(Design.Text("MODELO E RUNTIME",11,true));panel.Children.Add(Design.Text($"Modelo em uso: {vm.Settings.Model}\nModelo recomendado: gpt-oss:20b · OpenAI\nRuntime local: Ollama",13));
        panel.Children.Add(Design.Text("OPEN SOURCE E LICENÇAS",11,true));foreach(var item in new[]{"Avalonia UI · MIT","CommunityToolkit.Mvvm · MIT","Microsoft.Extensions.DependencyInjection · MIT","Microsoft.Data.Sqlite · MIT","Markdig · BSD 2-Clause","Open XML SDK · MIT","PDFsharp · MIT","PdfPig · Apache 2.0","Model Context Protocol C# SDK · MIT","Ollama · MIT"})panel.Children.Add(Design.Text(item,12));
        var close=Design.Button("Fechar");close.HorizontalAlignment=HorizontalAlignment.Right;close.Click+=(_,_)=>dialog.Close();panel.Children.Add(close);dialog.Content=new ScrollViewer{Content=panel,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};await dialog.ShowDialog(this);
    }
    private static void OpenUrl(string url)
    {try{Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}catch(Exception e){AppLog.Write(e);}}
    private async Task MoveConversationAsync()
    {
        var dialog=Dialog("Mover conversa");var panel=new StackPanel{Margin=new Thickness(24),Spacing=8};panel.Children.Add(Design.Text("Escolha uma pasta",21,true));
        var none=Design.Button("Sem pasta");none.HorizontalAlignment=HorizontalAlignment.Stretch;none.HorizontalContentAlignment=HorizontalAlignment.Left;none.Click+=(_,_)=>dialog.Close("");panel.Children.Add(none);
        foreach(var folder in vm.Folders){var button=Design.Button(folder.Name);button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Click+=(_,_)=>dialog.Close(folder.Id);panel.Children.Add(button);}dialog.Content=panel;var folderId=await dialog.ShowDialog<string?>(this);if(folderId is null)return;await Safe(()=>vm.MoveToFolderAsync(vm.Folders.FirstOrDefault(f=>f.Id==folderId)));
    }
    private Control MessageCard(MessageViewModel m)
    {
        var panel=new StackPanel{Spacing=10,Margin=new Thickness(0,16,0,22)};
        var head=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto")};head.Children.Add(Design.Text(m.IsUser?"VOCÊ":vm.Settings.AssistantName.ToUpperInvariant(),11,true));
        var copy=Design.Button("Copiar");copy.FontSize=11;copy.Padding=new Thickness(8,3);copy.MinHeight=28;Grid.SetColumn(copy,1);head.Children.Add(copy);copy.Click+=async(_,_)=>{if(Clipboard is {} c){await c.SetTextAsync(m.Content);copy.Content="Copiado";}};panel.Children.Add(head);
        var markdown=new MarkdownView();markdown.Render(m.Content);panel.Children.Add(markdown);
        var state=Design.Text("Resposta parcial · geração interrompida",11);state.IsVisible=m.State!="complete"&&!vm.Busy;panel.Children.Add(state);
        m.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(m.Content))markdown.Render(m.Content);state.IsVisible=m.State!="complete"&&!vm.Busy;};
        void UpdateState(object? sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName==nameof(vm.Busy))state.IsVisible=m.State!="complete"&&!vm.Busy;}
        panel.AttachedToVisualTree+=(_,_)=>vm.PropertyChanged+=UpdateState;
        panel.DetachedFromVisualTree+=(_,_)=>vm.PropertyChanged-=UpdateState;
        return panel;
    }
    private async Task Safe(Func<Task> action){try{await action();}catch(Exception e){vm.Report(e,"Não foi possível concluir a operação.");}}
    public async Task<string?> PromptAsync(string title,string initial)
    {
        var dialog=Dialog(title);var panel=new StackPanel{Margin=new Thickness(24),Spacing=16};var input=new TextBox{Text=initial,MaxLength=120};panel.Children.Add(Design.Text(title,20,true));panel.Children.Add(input);var save=Design.Button("Salvar");panel.Children.Add(save);save.Click+=(_,_)=>{if(!string.IsNullOrWhiteSpace(input.Text))dialog.Close(input.Text.Trim());};dialog.Content=panel;dialog.Opened+=(_,_)=>{input.Focus();input.SelectAll();};return await dialog.ShowDialog<string?>(this);
    }
    public async Task<bool> ConfirmAsync(string title,string text,string action)
    {
        var dialog=Dialog(title);var panel=new StackPanel{Margin=new Thickness(24),Spacing=16};panel.Children.Add(Design.Text(title,20,true));panel.Children.Add(Design.Text(text));var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};var cancel=Design.Button("Cancelar");cancel.Click+=(_,_)=>dialog.Close(false);var yes=Design.Button(action);yes.Click+=(_,_)=>dialog.Close(true);row.Children.Add(cancel);row.Children.Add(yes);panel.Children.Add(row);dialog.Content=panel;return await dialog.ShowDialog<bool>(this);
    }
    public static Window Dialog(string title)=>new(){Title=title,Width=470,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
}
