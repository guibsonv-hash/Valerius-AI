using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;
using ValeriusAI.Core;
using ValeriusAI.Infrastructure;
namespace ValeriusAI.App;

internal static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        try { BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch(Exception e) {AppLog.Write(e);}
    }
    public static AppBuilder BuildAvaloniaApp()=>AppBuilder.Configure<App>().UsePlatformDetect().With(new MacOSPlatformOptions {ShowInDock=true}).LogToTrace();
}
public static class AppLog
{
    public static string DataDirectory => OperatingSystem.IsMacOS()?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","Application Support",Product.Name):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Product.Name);
    public static void Write(Exception e)
    {
        try { Directory.CreateDirectory(DataDirectory);var path=Path.Combine(DataDirectory,"technical.log");if(File.Exists(path)&&new FileInfo(path).Length>1_000_000) File.Move(path,path+".previous",true);File.AppendAllText(path,$"{DateTimeOffset.Now:O} {e.GetType().Name} HResult={e.HResult}\n{e.StackTrace}\n"); } catch { /* O erro original permanece visível na interface. */ }
    }
}
public class App : Application
{
    public override void Initialize() {Name=Product.Name;Styles.Add(new FluentTheme());Styles.Add(Design.CreateStyles());RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Default;}
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try {Directory.CreateDirectory(AppLog.DataDirectory);}
            catch(Exception e)
            {
                AppLog.Write(e);
                desktop.MainWindow=new Avalonia.Controls.Window{Title=Product.Name,Width=520,Height=240,Content=new Avalonia.Controls.TextBlock{Text="Não foi possível abrir a pasta de dados. Confira as permissões de Library/Application Support e o espaço em disco, depois reabra o aplicativo.",TextWrapping=Avalonia.Media.TextWrapping.Wrap,Margin=new Avalonia.Thickness(30)}};
                base.OnFrameworkInitializationCompleted();return;
            }
            var services=new ServiceCollection();
            var repository=new SqliteChatRepository(Path.Combine(AppLog.DataDirectory,"valerius.db"));
            services.AddSingleton<IChatRepository>(repository);services.AddSingleton<IWorkspaceRepository>(repository);
            services.AddSingleton<IModelProvider>(new OllamaProvider(new HttpClient(new SocketsHttpHandler {UseProxy=false,AllowAutoRedirect=false}){BaseAddress=new Uri("http://127.0.0.1:11434/"),Timeout=Timeout.InfiniteTimeSpan}));
            services.AddSingleton<ILocalRuntime,OllamaRuntime>();
            services.AddSingleton<KnowledgeService>();services.AddSingleton<IContextAugmenter>(sp=>sp.GetRequiredService<KnowledgeService>());
            services.AddSingleton(sp=>new ArtifactService(sp.GetRequiredService<IWorkspaceRepository>(),Path.Combine(AppLog.DataDirectory,"Arquivos")));
            services.AddSingleton<WorkService>();services.AddSingleton<McpService>();services.AddSingleton<ChatService>();services.AddSingleton<MainViewModel>();
            var provider=services.BuildServiceProvider();desktop.MainWindow=new MainWindow(provider.GetRequiredService<MainViewModel>());desktop.Exit+=(_,_)=>provider.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
