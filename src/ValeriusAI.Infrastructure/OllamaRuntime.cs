using System.Diagnostics;
using ValeriusAI.Core;
namespace ValeriusAI.Infrastructure;
public sealed class OllamaRuntime(IModelProvider provider):ILocalRuntime
{
    public string InstallationUrl=>OperatingSystem.IsWindows()?"https://ollama.com/download/windows":"https://ollama.com/download/mac";
    public string? FindExecutable()
    {
        var candidates=new List<string>{"/opt/homebrew/bin/ollama","/usr/local/bin/ollama","/Applications/Ollama.app/Contents/Resources/ollama",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Ollama","ollama.exe")};
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Where(p=>!string.IsNullOrWhiteSpace(p)).Select(p=>Path.Combine(p,OperatingSystem.IsWindows()?"ollama.exe":"ollama")));
        return candidates.FirstOrDefault(File.Exists);
    }
    public async Task StartAsync()
    {
        if((await provider.CheckAvailabilityAsync()).Ready)return;
        var path=FindExecutable()??throw new ModelException("Ollama não instalado. Instale pelo site oficial e retorne aos Ajustes.");
        var info=new ProcessStartInfo(path,"serve"){UseShellExecute=false,CreateNoWindow=true};info.Environment["OLLAMA_HOST"]="127.0.0.1:11434";info.Environment["OLLAMA_NO_CLOUD"]="1";
        Process.Start(info)?.Dispose();
        for(var attempt=0;attempt<5;attempt++){await Task.Delay(600);if((await provider.CheckAvailabilityAsync()).Ready)return;}
        throw new ModelException("Ollama ainda não respondeu. Abra o aplicativo Ollama e atualize os modelos.");
    }
}
