using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json;
using ValeriusAI.Core;

namespace ValeriusAI.Infrastructure;

public sealed class SqliteChatRepository(string path) : IChatRepository, IWorkspaceRepository
{
    private const int SchemaVersion = 2;
    private readonly SemaphoreSlim gate = new(1, 1);

    private async Task<T> Run<T>(Func<SqliteConnection, T> action)
    {
        await gate.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                using var db = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    ForeignKeys = true,
                    DefaultTimeout = 5
                }.ToString());
                db.Open();
                return action(db);
            });
        }
        finally { gate.Release(); }
    }

    private static SqliteCommand Command(SqliteConnection db, string sql, params (string, object?)[] args)
    {
        var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static int Exec(SqliteConnection db, string sql, params (string, object?)[] args)
    { using var cmd = Command(db, sql, args); return cmd.ExecuteNonQuery(); }
    private static DateTimeOffset Date(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    private static string Iso(DateTimeOffset value) => value.ToString("O");
    private static bool Bool(SqliteDataReader reader, int index) => reader.GetInt64(index) != 0;

    public Task InitializeAsync() => Run(db =>
    {
        using var versionCommand = Command(db, "PRAGMA user_version");
        var version = Convert.ToInt32(versionCommand.ExecuteScalar());
        if (version > SchemaVersion) throw new InvalidOperationException("Banco criado por uma versão mais recente.");
        Exec(db, "PRAGMA journal_mode=WAL;");
        if (version == 0)
        {
            Exec(db, """
                CREATE TABLE Conversation(Id TEXT PRIMARY KEY,Title TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL);
                CREATE TABLE Message(Id TEXT PRIMARY KEY,ConversationId TEXT NOT NULL REFERENCES Conversation(Id) ON DELETE CASCADE,Role TEXT NOT NULL,Content TEXT NOT NULL,CreatedAt TEXT NOT NULL,State TEXT NOT NULL);
                CREATE INDEX IX_Message_Conversation ON Message(ConversationId,CreatedAt);
                CREATE TABLE Setting(Key TEXT PRIMARY KEY,Value TEXT NOT NULL);
                PRAGMA user_version=1;
                """);
            version = 1;
        }
        if (version == 1)
        {
            Exec(db, """
                ALTER TABLE Conversation ADD COLUMN FolderId TEXT;
                ALTER TABLE Conversation ADD COLUMN IsArchived INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE Conversation ADD COLUMN IsPinned INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE Conversation ADD COLUMN Mode TEXT NOT NULL DEFAULT 'chat';
                CREATE TABLE Folder(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,CreatedAt TEXT NOT NULL);
                CREATE TABLE Memory(Id TEXT PRIMARY KEY,Content TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,Enabled INTEGER NOT NULL DEFAULT 1);
                CREATE TABLE Document(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Path TEXT NOT NULL,Type TEXT NOT NULL,Size INTEGER NOT NULL,Status TEXT NOT NULL,CreatedAt TEXT NOT NULL,Error TEXT);
                CREATE TABLE DocumentChunk(Id TEXT PRIMARY KEY,DocumentId TEXT NOT NULL REFERENCES Document(Id) ON DELETE CASCADE,Position INTEGER NOT NULL,Content TEXT NOT NULL,EmbeddingJson TEXT NOT NULL);
                CREATE INDEX IX_Chunk_Document ON DocumentChunk(DocumentId,Position);
                CREATE TABLE McpServer(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Transport TEXT NOT NULL,Command TEXT NOT NULL,Arguments TEXT NOT NULL,Enabled INTEGER NOT NULL,Status TEXT NOT NULL);
                CREATE TABLE ToolSetting(Name TEXT PRIMARY KEY,Enabled INTEGER NOT NULL,Permission TEXT NOT NULL);
                CREATE TABLE AgentTask(Id TEXT PRIMARY KEY,Goal TEXT NOT NULL,Status TEXT NOT NULL,CreatedAt TEXT NOT NULL,UpdatedAt TEXT NOT NULL,ConversationId TEXT);
                CREATE TABLE AgentStep(Id TEXT PRIMARY KEY,TaskId TEXT NOT NULL REFERENCES AgentTask(Id) ON DELETE CASCADE,Position INTEGER NOT NULL,Description TEXT NOT NULL,Status TEXT NOT NULL,Result TEXT);
                CREATE TABLE Artifact(Id TEXT PRIMARY KEY,TaskId TEXT NOT NULL REFERENCES AgentTask(Id) ON DELETE CASCADE,Name TEXT NOT NULL,Path TEXT NOT NULL,Type TEXT NOT NULL,Size INTEGER NOT NULL,CreatedAt TEXT NOT NULL);
                CREATE INDEX IX_Artifact_Task ON Artifact(TaskId,CreatedAt);
                PRAGMA user_version=2;
                """);
        }
        return 0;
    });

    public Task<IReadOnlyList<Conversation>> GetConversationsAsync() => Run<IReadOnlyList<Conversation>>(db =>
    {
        using var cmd = Command(db, "SELECT Id,Title,CreatedAt,UpdatedAt,FolderId,IsArchived,IsPinned,Mode FROM Conversation ORDER BY IsPinned DESC,UpdatedAt DESC");
        using var r = cmd.ExecuteReader(); var list = new List<Conversation>();
        while (r.Read()) list.Add(new(r.GetString(0), r.GetString(1), Date(r.GetString(2)), Date(r.GetString(3)), r.IsDBNull(4) ? null : r.GetString(4), Bool(r, 5), Bool(r, 6), r.GetString(7)));
        return list;
    });

    public Task<Conversation> CreateAsync(string title, string mode = "chat") => Run(db =>
    {
        if (mode is not ("chat" or "create")) throw new ArgumentException("Escolha um modo de conversa válido.");
        var now = DateTimeOffset.UtcNow;
        var item = new Conversation(Guid.NewGuid().ToString(), title, now, now, Mode: mode);
        Exec(db, "INSERT INTO Conversation(Id,Title,CreatedAt,UpdatedAt,FolderId,IsArchived,IsPinned,Mode) VALUES($id,$title,$created,$updated,NULL,0,0,$mode)",
            ("$id", item.Id), ("$title", item.Title), ("$created", Iso(now)), ("$updated", Iso(now)), ("$mode", item.Mode));
        return item;
    });

    public Task RenameAsync(string id, string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Informe um título.");
        return Run(db => Exec(db, "UPDATE Conversation SET Title=$title,UpdatedAt=$now WHERE Id=$id", ("$title", title.Trim()), ("$now", Iso(DateTimeOffset.UtcNow)), ("$id", id)));
    }
    public Task UpdateConversationAsync(Conversation c) => Run(db => Exec(db, "UPDATE Conversation SET Title=$title,FolderId=$folder,IsArchived=$archived,IsPinned=$pinned,Mode=$mode,UpdatedAt=$updated WHERE Id=$id",
        ("$title", c.Title), ("$folder", c.FolderId), ("$archived", c.IsArchived), ("$pinned", c.IsPinned), ("$mode", c.Mode), ("$updated", Iso(c.UpdatedAt)), ("$id", c.Id)));
    public Task DeleteAsync(string id) => Run(db => Exec(db, "DELETE FROM Conversation WHERE Id=$id", ("$id", id)));
    public Task ClearAsync() => Run(db => Exec(db, "DELETE FROM Conversation"));
    public Task<IReadOnlyList<Message>> GetMessagesAsync(string id) => Run<IReadOnlyList<Message>>(db =>
    {
        using var cmd = Command(db, "SELECT Id,ConversationId,Role,Content,CreatedAt,State FROM Message WHERE ConversationId=$id ORDER BY CreatedAt,rowid", ("$id", id));
        using var r = cmd.ExecuteReader(); var list = new List<Message>();
        while (r.Read()) list.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), Date(r.GetString(4)), r.GetString(5)));
        return list;
    });
    public Task SaveMessageAsync(Message m) => Run(db =>
    {
        Exec(db, "INSERT INTO Message VALUES($id,$cid,$role,$content,$created,$state) ON CONFLICT(Id) DO UPDATE SET Content=excluded.Content,State=excluded.State",
            ("$id", m.Id), ("$cid", m.ConversationId), ("$role", m.Role), ("$content", m.Content), ("$created", Iso(m.CreatedAt)), ("$state", m.State));
        Exec(db, "UPDATE Conversation SET UpdatedAt=$now WHERE Id=$id", ("$now", Iso(DateTimeAndNow())), ("$id", m.ConversationId));
        return 0;
    });
    private static DateTimeOffset DateTimeAndNow() => DateTimeOffset.UtcNow;
    public Task<AppSettings> GetSettingsAsync() => Run(db =>
    {
        using var cmd = Command(db, "SELECT Value FROM Setting WHERE Key='app'");
        var json = cmd.ExecuteScalar() as string;
        return json is null ? new() : JsonSerializer.Deserialize<AppSettings>(json) ?? new();
    });
    public Task SaveSettingsAsync(AppSettings s)
    {
        s.Validate();
        return Run(db => Exec(db, "INSERT INTO Setting VALUES('app',$json) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value", ("$json", JsonSerializer.Serialize(s))));
    }

    public Task<IReadOnlyList<Folder>> GetFoldersAsync() => Run<IReadOnlyList<Folder>>(db =>
    { using var c = Command(db, "SELECT Id,Name,CreatedAt FROM Folder ORDER BY Name COLLATE NOCASE"); using var r = c.ExecuteReader(); var x = new List<Folder>(); while (r.Read()) x.Add(new(r.GetString(0), r.GetString(1), Date(r.GetString(2)))); return x; });
    public Task<Folder> SaveFolderAsync(Folder folder) => Run(db =>
    { Exec(db, "INSERT INTO Folder VALUES($id,$name,$created) ON CONFLICT(Id) DO UPDATE SET Name=excluded.Name", ("$id", folder.Id), ("$name", folder.Name), ("$created", Iso(folder.CreatedAt))); return folder; });
    public Task DeleteFolderAsync(string id) => Run(db =>
    { using var transaction=db.BeginTransaction();Exec(db, "UPDATE Conversation SET FolderId=NULL WHERE FolderId=$id", ("$id", id));var changed=Exec(db, "DELETE FROM Folder WHERE Id=$id", ("$id", id));transaction.Commit();return changed; });

    public Task<IReadOnlyList<MemoryItem>> GetMemoriesAsync() => Run<IReadOnlyList<MemoryItem>>(db =>
    { using var c = Command(db, "SELECT Id,Content,CreatedAt,UpdatedAt,Enabled FROM Memory ORDER BY UpdatedAt DESC"); using var r = c.ExecuteReader(); var x = new List<MemoryItem>(); while (r.Read()) x.Add(new(r.GetString(0), r.GetString(1), Date(r.GetString(2)), Date(r.GetString(3)), Bool(r, 4))); return x; });
    public Task SaveMemoryAsync(MemoryItem m) => Run(db => Exec(db, "INSERT INTO Memory VALUES($id,$content,$created,$updated,$enabled) ON CONFLICT(Id) DO UPDATE SET Content=excluded.Content,UpdatedAt=excluded.UpdatedAt,Enabled=excluded.Enabled", ("$id", m.Id), ("$content", m.Content), ("$created", Iso(m.CreatedAt)), ("$updated", Iso(m.UpdatedAt)), ("$enabled", m.Enabled)));
    public Task DeleteMemoryAsync(string id) => Run(db => Exec(db, "DELETE FROM Memory WHERE Id=$id", ("$id", id)));

    public Task<IReadOnlyList<KnowledgeDocument>> GetDocumentsAsync() => Run<IReadOnlyList<KnowledgeDocument>>(db =>
    { using var c = Command(db, "SELECT Id,Name,Path,Type,Size,Status,CreatedAt,Error FROM Document ORDER BY CreatedAt DESC"); using var r = c.ExecuteReader(); var x = new List<KnowledgeDocument>(); while (r.Read()) x.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetString(5), Date(r.GetString(6)), r.IsDBNull(7) ? null : r.GetString(7))); return x; });
    public Task SaveDocumentAsync(KnowledgeDocument d, IReadOnlyList<KnowledgeChunk> chunks) => Run(db =>
    {
        using var tx = db.BeginTransaction();
        Exec(db, "INSERT INTO Document VALUES($id,$name,$path,$type,$size,$status,$created,$error) ON CONFLICT(Id) DO UPDATE SET Name=excluded.Name,Path=excluded.Path,Type=excluded.Type,Size=excluded.Size,Status=excluded.Status,Error=excluded.Error",
            ("$id", d.Id), ("$name", d.Name), ("$path", d.Path), ("$type", d.Type), ("$size", d.Size), ("$status", d.Status), ("$created", Iso(d.CreatedAt)), ("$error", d.Error));
        Exec(db, "DELETE FROM DocumentChunk WHERE DocumentId=$id", ("$id", d.Id));
        foreach (var chunk in chunks) Exec(db, "INSERT INTO DocumentChunk VALUES($id,$document,$position,$content,$embedding)", ("$id", chunk.Id), ("$document", chunk.DocumentId), ("$position", chunk.Position), ("$content", chunk.Content), ("$embedding", chunk.EmbeddingJson));
        tx.Commit(); return 0;
    });
    public Task DeleteDocumentAsync(string id) => Run(db => Exec(db, "DELETE FROM Document WHERE Id=$id", ("$id", id)));
    public Task<IReadOnlyList<KnowledgeChunk>> GetChunksAsync() => Run<IReadOnlyList<KnowledgeChunk>>(db =>
    { using var c = Command(db, "SELECT Id,DocumentId,Position,Content,EmbeddingJson FROM DocumentChunk"); using var r = c.ExecuteReader(); var x = new List<KnowledgeChunk>(); while (r.Read()) x.Add(new(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4))); return x; });

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query) => Run<IReadOnlyList<SearchHit>>(db =>
    {
        var pattern = $"%{query.Trim()}%"; var hits = new List<SearchHit>();
        using (var c = Command(db, "SELECT Id,Title,UpdatedAt FROM Conversation WHERE Title LIKE $q ESCAPE '\\' ORDER BY UpdatedAt DESC LIMIT 20", ("$q", pattern)))
        using (var r = c.ExecuteReader()) while (r.Read()) hits.Add(new("Conversa", r.GetString(0), r.GetString(1), r.GetString(1), Date(r.GetString(2))));
        using (var c = Command(db, "SELECT m.ConversationId,c.Title,m.Content,c.UpdatedAt FROM Message m JOIN Conversation c ON c.Id=m.ConversationId WHERE m.Content LIKE $q ESCAPE '\\' ORDER BY m.CreatedAt DESC LIMIT 30", ("$q", pattern)))
        using (var r = c.ExecuteReader()) while (r.Read()) hits.Add(new("Mensagem", r.GetString(0), r.GetString(1), Snip(r.GetString(2), query), Date(r.GetString(3))));
        using (var c = Command(db, "SELECT Id,Content,UpdatedAt FROM Memory WHERE Content LIKE $q ESCAPE '\\' ORDER BY UpdatedAt DESC LIMIT 20", ("$q", pattern)))
        using (var r = c.ExecuteReader()) while (r.Read()) hits.Add(new("Memória", r.GetString(0), "Memória", Snip(r.GetString(1), query), Date(r.GetString(2))));
        using (var c = Command(db, "SELECT d.Id,d.Name,k.Content,d.CreatedAt FROM DocumentChunk k JOIN Document d ON d.Id=k.DocumentId WHERE k.Content LIKE $q ESCAPE '\\' LIMIT 30", ("$q", pattern)))
        using (var r = c.ExecuteReader()) while (r.Read()) hits.Add(new("Conhecimento", r.GetString(0), r.GetString(1), Snip(r.GetString(2), query), Date(r.GetString(3))));
        return hits.OrderByDescending(x => x.UpdatedAt).Take(50).ToList();
    });
    private static string Snip(string text, string query)
    { var i = text.IndexOf(query, StringComparison.OrdinalIgnoreCase); var start = Math.Max(0, i - 60); return text.Substring(start, Math.Min(180, text.Length - start)).Replace('\n', ' '); }

    public Task<IReadOnlyList<McpServer>> GetMcpServersAsync() => Run<IReadOnlyList<McpServer>>(db =>
    { using var c = Command(db, "SELECT Id,Name,Transport,Command,Arguments,Enabled,Status FROM McpServer ORDER BY Name"); using var r = c.ExecuteReader(); var x = new List<McpServer>(); while (r.Read()) x.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), Bool(r, 5), r.GetString(6))); return x; });
    public Task SaveMcpServerAsync(McpServer s) => Run(db => Exec(db, "INSERT INTO McpServer VALUES($id,$name,$transport,$command,$args,$enabled,$status) ON CONFLICT(Id) DO UPDATE SET Name=excluded.Name,Transport=excluded.Transport,Command=excluded.Command,Arguments=excluded.Arguments,Enabled=excluded.Enabled,Status=excluded.Status", ("$id", s.Id), ("$name", s.Name), ("$transport", s.Transport), ("$command", s.Command), ("$args", s.Arguments), ("$enabled", s.Enabled), ("$status", s.Status)));
    public Task DeleteMcpServerAsync(string id) => Run(db => Exec(db, "DELETE FROM McpServer WHERE Id=$id", ("$id", id)));

    public Task<IReadOnlyList<ToolSetting>> GetToolSettingsAsync() => Run<IReadOnlyList<ToolSetting>>(db =>
    { using var c = Command(db, "SELECT Name,Enabled,Permission FROM ToolSetting ORDER BY Name"); using var r = c.ExecuteReader(); var x = new List<ToolSetting>(); while (r.Read()) x.Add(new(r.GetString(0), Bool(r, 1), r.GetString(2))); return x; });
    public Task SaveToolSettingAsync(ToolSetting s) => Run(db => Exec(db, "INSERT INTO ToolSetting VALUES($name,$enabled,$permission) ON CONFLICT(Name) DO UPDATE SET Enabled=excluded.Enabled,Permission=excluded.Permission", ("$name", s.Name), ("$enabled", s.Enabled), ("$permission", s.Permission)));

    public Task SaveAgentTaskAsync(AgentTask t, IReadOnlyList<AgentStep> steps) => Run(db =>
    {
        using var tx = db.BeginTransaction();
        Exec(db, "INSERT INTO AgentTask VALUES($id,$goal,$status,$created,$updated,$conversation) ON CONFLICT(Id) DO UPDATE SET Goal=excluded.Goal,Status=excluded.Status,UpdatedAt=excluded.UpdatedAt", ("$id", t.Id), ("$goal", t.Goal), ("$status", t.Status), ("$created", Iso(t.CreatedAt)), ("$updated", Iso(t.UpdatedAt)), ("$conversation", t.ConversationId));
        foreach (var s in steps) Exec(db, "INSERT INTO AgentStep VALUES($id,$task,$position,$description,$status,$result) ON CONFLICT(Id) DO UPDATE SET Status=excluded.Status,Result=excluded.Result", ("$id", s.Id), ("$task", s.TaskId), ("$position", s.Position), ("$description", s.Description), ("$status", s.Status), ("$result", s.Result));
        tx.Commit(); return 0;
    });
    public Task<IReadOnlyList<AgentTask>> GetAgentTasksAsync() => Run<IReadOnlyList<AgentTask>>(db =>
    { using var c = Command(db, "SELECT Id,Goal,Status,CreatedAt,UpdatedAt,ConversationId FROM AgentTask ORDER BY UpdatedAt DESC"); using var r = c.ExecuteReader(); var x = new List<AgentTask>(); while (r.Read()) x.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), Date(r.GetString(3)), Date(r.GetString(4)), r.IsDBNull(5) ? null : r.GetString(5))); return x; });
    public Task<IReadOnlyList<AgentStep>> GetAgentStepsAsync(string taskId) => Run<IReadOnlyList<AgentStep>>(db =>
    { using var c = Command(db, "SELECT Id,TaskId,Position,Description,Status,Result FROM AgentStep WHERE TaskId=$id ORDER BY Position", ("$id", taskId)); using var r = c.ExecuteReader(); var x = new List<AgentStep>(); while (r.Read()) x.Add(new(r.GetString(0), r.GetString(1), r.GetInt32(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5))); return x; });
    public Task SaveArtifactAsync(Artifact a) => Run(db => Exec(db, "INSERT INTO Artifact VALUES($id,$task,$name,$path,$type,$size,$created) ON CONFLICT(Id) DO UPDATE SET Name=excluded.Name,Path=excluded.Path,Type=excluded.Type,Size=excluded.Size", ("$id", a.Id), ("$task", a.TaskId), ("$name", a.Name), ("$path", a.Path), ("$type", a.Type), ("$size", a.Size), ("$created", Iso(a.CreatedAt))));
    public Task DeleteArtifactAsync(string id) => Run(db => Exec(db, "DELETE FROM Artifact WHERE Id=$id", ("$id", id)));
    public Task<IReadOnlyList<Artifact>> GetArtifactsAsync() => Run<IReadOnlyList<Artifact>>(db =>
    { using var c = Command(db, "SELECT Id,TaskId,Name,Path,Type,Size,CreatedAt FROM Artifact ORDER BY CreatedAt DESC"); using var r = c.ExecuteReader(); var x = new List<Artifact>(); while (r.Read()) x.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt64(5), Date(r.GetString(6)))); return x; });
}
