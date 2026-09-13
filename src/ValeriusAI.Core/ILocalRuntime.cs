namespace ValeriusAI.Core;
public interface ILocalRuntime
{
    string? FindExecutable();
    string InstallationUrl { get; }
    Task StartAsync();
}
