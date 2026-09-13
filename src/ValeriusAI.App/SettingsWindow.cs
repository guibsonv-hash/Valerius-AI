using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using System.Diagnostics;
using ValeriusAI.Core;
namespace ValeriusAI.App;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(MainViewModel vm)
    {
        Title="Ajustes · "+Product.Name;Width=610;Height=760;MinWidth=500;MinHeight=520;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        this.Bind(BackgroundProperty,new DynamicResourceExtension("Page"));this.Bind(ForegroundProperty,new DynamicResourceExtension("Ink"));
        var root=new Grid{RowDefinitions=new RowDefinitions("*,Auto")};Content=root;
        var body=new StackPanel{Spacing=14,Margin=new Thickness(28)};root.Children.Add(new ScrollViewer{Content=body,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled});
        body.Children.Add(Design.Text("Seu assistente, do seu jeito.",25,true));body.Children.Add(Design.Text("Configuração local · sem conta · sem telemetria",13));
        void Section(string label){body.Children.Add(new Separator{Margin=new Thickness(0,10)});body.Children.Add(Design.Text(label,18,true));}
        void Field(string label,Control control){body.Children.Add(Design.Text(label,13,true));Avalonia.Automation.AutomationProperties.SetName(control,label);body.Children.Add(control);}
        Section("Geral");var name=new TextBox{Text=vm.Settings.AssistantName,MaxLength=60};Field("Nome do assistente",name);
        var theme=new ChoiceBox{ItemsSource=new[]{"Sistema","Claro","Escuro"},SelectedItem=vm.Settings.Theme,HorizontalAlignment=HorizontalAlignment.Stretch};Field("Aparência",theme);
        Section("Modelo local");var status=Design.Text(vm.Ready?"Ollama conectado · inferência local":string.IsNullOrEmpty(vm.Error)?vm.Status:vm.Error,13);body.Children.Add(status);
        var model=new ChoiceBox{ItemsSource=vm.Models,SelectedItem=vm.Models.FirstOrDefault(m=>m.Name==vm.Settings.Model),HorizontalAlignment=HorizontalAlignment.Stretch};Field("Modelo instalado",model);
        var runtime=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};var refresh=Design.Button("Atualizar modelos");refresh.Click+=async(_,_)=>{refresh.IsEnabled=false;await vm.RefreshAsync();model.SelectedItem=vm.Models.FirstOrDefault(m=>m.Name==vm.Settings.Model);refresh.IsEnabled=true;};runtime.Children.Add(refresh);
        var start=Design.Button("Iniciar Ollama");start.Click+=async(_,_)=>{start.IsEnabled=false;await vm.StartRuntimeAsync();start.IsEnabled=true;};runtime.Children.Add(start);body.Children.Add(runtime);
        var install=Design.Button("Instalar Ollama ↗");install.Click+=(_,_)=>{try{Process.Start(new ProcessStartInfo(vm.RuntimeInstallationUrl){UseShellExecute=true});}catch(Exception e){vm.Report(e,"Abra ollama.com/download/mac no navegador.");}};body.Children.Add(install);
        long memory=GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var disk=new DriveInfo(Path.GetPathRoot(AppLog.DataDirectory)!).AvailableFreeSpace;
        body.Children.Add(Design.Text($"Padrão oficial: {Product.RecommendedModel} · aproximadamente 14 GB. Requer cerca de 16 GB de memória unificada e espaço livre suficiente. Disponível agora: {disk/1e9:0.0} GB em disco e {memory/1073741824.0:0.#} GB para o runtime.",13));
        var pull=Design.Button("Baixar GPT OSS 20B · 14 GB");pull.IsEnabled=disk>Product.RecommendedModelBytes+2_000_000_000;pull.Click+=async(_,_)=>await vm.DownloadAsync();body.Children.Add(pull);
        if(!pull.IsEnabled)body.Children.Add(Design.Text("Libere pelo menos 16 GB para habilitar este download. Você pode usar outro modelo Ollama já instalado.",12));
        var progress=new ProgressBar{Minimum=0,Maximum=100,Height=5};body.Children.Add(progress);var downloadStatus=Design.Text(vm.DownloadStatus,12);body.Children.Add(downloadStatus);
        var temperature=new NumericUpDown{Minimum=0,Maximum=2,Increment=0.1m,Value=(decimal)vm.Settings.Temperature,FormatString="0.0"};Field("Temperatura · criatividade",temperature);
        var reasoning=new ChoiceBox{ItemsSource=new[]{"Rápido","Equilibrado","Profundo"},SelectedItem=vm.Settings.ReasoningEffort switch{"low"=>"Rápido","high"=>"Profundo",_=>"Equilibrado"},HorizontalAlignment=HorizontalAlignment.Stretch};Field("Nível de raciocínio",reasoning);
        var tokens=new NumericUpDown{Minimum=64,Maximum=8192,Increment=256,Value=vm.Settings.MaxTokens};Field("Limite da resposta · tokens",tokens);
        var context=new NumericUpDown{Minimum=2048,Maximum=131072,Increment=2048,Value=vm.Settings.ContextSize};Field("Janela de contexto · tokens",context);body.Children.Add(Design.Text("Mais contexto usa mais memória. Conversas longas usam o trecho recente que cabe no limite; o histórico completo permanece salvo.",12));
        var useMemory=new CheckBox{Content="Usar memória pessoal no chat",IsChecked=vm.Settings.UseMemory};body.Children.Add(useMemory);
        var useKnowledge=new CheckBox{Content="Consultar a base de conhecimento",IsChecked=vm.Settings.UseKnowledge};body.Children.Add(useKnowledge);
        Section("Personalidade");var system=new TextBox{Text=vm.Settings.SystemPrompt,AcceptsReturn=true,TextWrapping=Avalonia.Media.TextWrapping.Wrap,MinHeight=145,MaxHeight=260,MaxLength=16000,VerticalContentAlignment=VerticalAlignment.Top};Field("System Prompt global",system);
        Section("Privacidade");body.Children.Add(Design.Text("Suas conversas e inferências permanecem neste computador quando um modelo local está sendo utilizado. Este aplicativo aceita apenas modelos locais e não envia telemetria.",14));body.Children.Add(Design.Text("O histórico é salvo em SQLite sem criptografia. A proteção do computador e do FileVault se aplica a esses arquivos.",12));
        var clear=Design.Button("Limpar todas as conversas…");clear.Click+=async(_,_)=>
        {
            var dialog=MainWindow.Dialog("Limpar histórico");var content=new StackPanel{Margin=new Thickness(24),Spacing=16};content.Children.Add(Design.Text("Excluir todas as conversas?",21,true));content.Children.Add(Design.Text("Esta ação remove todo o histórico local. As configurações e os modelos serão preservados."));var cancel=Design.Button("Cancelar");cancel.Click+=(_,_)=>dialog.Close(false);var yes=Design.Button("Excluir todo o histórico");yes.Click+=(_,_)=>dialog.Close(true);content.Children.Add(cancel);content.Children.Add(yes);dialog.Content=content;
            if(await dialog.ShowDialog<bool>(this))try{await vm.ClearAsync();}catch(Exception e){vm.Report(e,"Não foi possível limpar o histórico.");}
        };body.Children.Add(clear);
        Section("Recursos avançados");body.Children.Add(Design.Text("Ferramentas e integrações ficam aqui para manter a tela principal simples.",12));
        var advanced=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};var tools=Design.Button("Ferramentas locais");tools.Click+=(_,_)=>{vm.Navigate("Tools");Close();};advanced.Children.Add(tools);var mcp=Design.Button("Integrações MCP");mcp.Click+=(_,_)=>{vm.Navigate("MCP");Close();};advanced.Children.Add(mcp);body.Children.Add(advanced);
        var licenses=Design.Button("Créditos e licenças");licenses.Click+=async(_,_)=>await ShowLicensesAsync();body.Children.Add(licenses);
        var bottom=new StackPanel{Spacing=8,Margin=new Thickness(24,12)};Grid.SetRow(bottom,1);root.Children.Add(bottom);var error=Design.Text("",13);bottom.Children.Add(error);var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};bottom.Children.Add(actions);var close=Design.Button("Fechar");close.Click+=(_,_)=>Close();actions.Children.Add(close);var save=Design.Button("Salvar ajustes");actions.Children.Add(save);
        save.Click+=async(_,_)=>{try{save.IsEnabled=false;await vm.SaveSettingsAsync(new AppSettings{AssistantName=name.Text?.Trim()??"",Theme=theme.SelectedItem as string??"Sistema",Model=(model.SelectedItem as LocalModel)?.Name??vm.Settings.Model,Temperature=(double)(temperature.Value??0.7m),MaxTokens=(int)(tokens.Value??2048),ContextSize=(int)(context.Value??8192),ReasoningEffort=reasoning.SelectedItem?.ToString() switch{"Rápido"=>"low","Profundo"=>"high",_=>"medium"},UseMemory=useMemory.IsChecked==true,UseKnowledge=useKnowledge.IsChecked==true,EmbeddingModel=vm.Settings.EmbeddingModel,SystemPrompt=system.Text??""});Close();}catch(Exception e){error.Text=e is ArgumentException?e.Message:"Não foi possível salvar os ajustes. Confira o banco local.";AppLog.Write(e);}finally{save.IsEnabled=true;}};
        void Changed(object? _,System.ComponentModel.PropertyChangedEventArgs e)
        {status.Text=vm.Ready?"Ollama conectado · inferência local":string.IsNullOrEmpty(vm.Error)?vm.Status:vm.Error;downloadStatus.Text=vm.DownloadStatus;progress.Value=vm.DownloadPercent;pull.Content=vm.Downloading?"Interromper download":"Baixar GPT OSS 20B · 14 GB";if(model.SelectedItem is null&&vm.Models.Count>0)model.SelectedItem=vm.Models.FirstOrDefault(m=>m.Name==vm.Settings.Model);}
        vm.PropertyChanged+=Changed;Closed+=(_,_)=>vm.PropertyChanged-=Changed;
    }
    private async Task ShowLicensesAsync()
    {
        var dialog=MainWindow.Dialog("Créditos e licenças");dialog.Width=560;var panel=new StackPanel{Margin=new Thickness(24),Spacing=12};panel.Children.Add(Design.Text("Créditos e licenças",22,true));panel.Children.Add(Design.Text("Valerius AI usa componentes de código aberto. Os textos completos das licenças acompanham os respectivos pacotes.",13));
        foreach(var item in new[]{"Avalonia UI · MIT","CommunityToolkit.Mvvm · MIT","Microsoft.Extensions.DependencyInjection · MIT","Microsoft.Data.Sqlite · MIT","Markdig · BSD 2-Clause","Open XML SDK · MIT","PDFsharp · MIT","PdfPig · Apache 2.0","Model Context Protocol C# SDK · MIT"})panel.Children.Add(Design.Text(item,13));var close=Design.Button("Fechar");close.Click+=(_,_)=>dialog.Close();panel.Children.Add(close);dialog.Content=new ScrollViewer{Content=panel};await dialog.ShowDialog(this);
    }
}
