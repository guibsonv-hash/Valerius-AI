using ModelContextProtocol.Client;
using ValeriusAI.Core;

namespace ValeriusAI.Infrastructure;

public sealed record McpToolInfo(string Name, string Description);

public sealed class McpService
{
    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(McpServer server, CancellationToken ct = default)
    {
        Validate(server);
        await using var client = await ConnectAsync(server, ct);
        var tools = await client.ListToolsAsync(cancellationToken: ct);
        return tools.Select(x => new McpToolInfo(x.Name, x.Description ?? "Ferramenta MCP")).ToList();
    }

    public async Task<string> CallAsync(McpServer server, string tool, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        Validate(server);
        await using var client = await ConnectAsync(server, ct);
        var result = await client.CallToolAsync(tool, arguments, cancellationToken: ct);
        return string.Join("\n", result.Content.Select(x => x.ToString()));
    }

    private static async Task<McpClient> ConnectAsync(McpServer server, CancellationToken ct)
    {
        var options = new StdioClientTransportOptions { Name = server.Name, Command = server.Command, Arguments = Split(server.Arguments) };
        return await McpClient.CreateAsync(new StdioClientTransport(options), cancellationToken: ct);
    }

    private static void Validate(McpServer server)
    {
        if (!server.Enabled) throw new InvalidOperationException("Ative o servidor MCP antes de conectar.");
        if (!server.Transport.Equals("stdio", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("Esta versão aceita servidores MCP por stdio.");
        if (string.IsNullOrWhiteSpace(server.Command)) throw new ArgumentException("Informe o comando do servidor MCP.");
    }

    private static string[] Split(string text) => string.IsNullOrWhiteSpace(text) ? [] : text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
