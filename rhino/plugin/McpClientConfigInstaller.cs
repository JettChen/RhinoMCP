using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace RhMcp;

// Adds the bundled rhino MCP server to the configs of MCP-aware tools the user has (Claude Code,
// Cursor, Codex, ...) so those external agents can drive Rhino without hand-copying the snippet.
// Detection and install are per-client and user-initiated from the Connect dialog's Install tab.
// We never create configs for tools the user doesn't run (that would litter the home dir), and
// injection is idempotent (a no-op once a `rhino` entry is present) and never overwrites one.
internal static class McpClientConfigInstaller
{
    internal enum McpConfigFormat
    {
        // mcpServers: { rhino: { command } }  - Claude Code, Cursor, Gemini, Windsurf.
        StandardJson,

        // mcp: { rhino: { type: "local", command: [...], enabled: true } }  - OpenCode's shape.
        OpenCodeJson,

        // [mcp_servers.rhino] command = "..."  - Codex's TOML.
        CodexToml,
    }

    internal sealed record McpClient(string DisplayName, string RelativePath, McpConfigFormat Format);

    internal enum McpInstallState { Missing, Detected, Installed }

    internal enum McpInstallResult { Installed, AlreadyInstalled, Missing, Unsupported, Failed }

    internal static IReadOnlyList<McpClient> Clients { get; } =
    [
        new("Claude Code", ".claude.json", McpConfigFormat.StandardJson),
        new("Cursor", ".cursor/mcp.json", McpConfigFormat.StandardJson),
        new("Gemini CLI", ".gemini/settings.json", McpConfigFormat.StandardJson),
        new("Windsurf", ".codeium/windsurf/mcp_config.json", McpConfigFormat.StandardJson),
        new("OpenCode", ".config/opencode/opencode.json", McpConfigFormat.OpenCodeJson),
        new("Codex", ".codex/config.toml", McpConfigFormat.CodexToml),
    ];

    // Cheap enough to call per grid row on every visit to the Install tab: a File.Exists plus one
    // small read. An unreadable config counts as Detected so the user can still attempt the install
    // (which reports its own error) rather than silently vanishing from the grid.
    public static McpInstallState GetState(McpClient client)
    {
        string path = ResolvePath(client.RelativePath);
        if (!File.Exists(path))
            return McpInstallState.Missing;

        try
        {
            return HasRhinoEntry(client, File.ReadAllText(path))
                ? McpInstallState.Installed
                : McpInstallState.Detected;
        }
        catch
        {
            return McpInstallState.Detected;
        }
    }

    public static McpInstallResult Install(McpClient client)
    {
        string path = ResolvePath(client.RelativePath);
        if (!File.Exists(path))
            return McpInstallResult.Missing;

        try
        {
            string original = File.ReadAllText(path);
            if (HasRhinoEntry(client, original))
                return McpInstallResult.AlreadyInstalled;

            // null now means only "shape we can't safely amend": the already-present case is handled above.
            string? updated = BuildUpdated(client, original);
            if (updated is null)
                return McpInstallResult.Unsupported;

            WriteAtomic(path, updated);
            Log($"wired the Rhino MCP server into {client.DisplayName} ({path})");
            return McpInstallResult.Installed;
        }
        catch (Exception ex)
        {
            Log($"failed to update {client.DisplayName}: {ex.Message}");
            return McpInstallResult.Failed;
        }
    }

    private static bool HasRhinoEntry(McpClient client, string content) => client.Format switch
    {
        McpConfigFormat.StandardJson => JsonHasEntry(content, "mcpServers"),
        McpConfigFormat.OpenCodeJson => JsonHasEntry(content, "mcp"),
        McpConfigFormat.CodexToml => Regex.IsMatch(content, TomlEntryPattern, RegexOptions.Multiline),
        _ => false,
    };

    private static bool JsonHasEntry(string content, string containerKey)
    {
        JsonNode? root = JsonNode.Parse(content, documentOptions: LenientJson);
        return root is JsonObject obj
            && obj[containerKey] is JsonObject servers
            && servers.ContainsKey(RouterMcpConfig.ServerName);
    }

    private static string? BuildUpdated(McpClient client, string original) => client.Format switch
    {
        McpConfigFormat.StandardJson => AddToJson(original, "mcpServers", StandardEntry),
        McpConfigFormat.OpenCodeJson => AddToJson(original, "mcp", OpenCodeEntry),
        McpConfigFormat.CodexToml => AddToToml(original),
        _ => null,
    };

    private static string? AddToJson(string original, string containerKey, Func<JsonNode> entry)
    {
        JsonNode? root = JsonNode.Parse(original, documentOptions: LenientJson);
        if (root is not JsonObject obj)
            return null;

        switch (obj[containerKey])
        {
            // Mutate the existing map in place: reassigning an already-parented JsonNode throws.
            case JsonObject servers when servers.ContainsKey(RouterMcpConfig.ServerName):
                return null;
            case JsonObject servers:
                servers[RouterMcpConfig.ServerName] = entry();
                break;
            case null:
                obj[containerKey] = new JsonObject { [RouterMcpConfig.ServerName] = entry() };
                break;
            default:
                return null; // present but not a server map; don't risk clobbering it.
        }

        return obj.ToJsonString(IndentedJson);
    }

    // No TOML parser is bundled, so detect the section header textually and append the table if
    // it's absent. A top-level table appended at EOF is always valid TOML regardless of what
    // precedes it, which makes this safe without a full parse.
    private static string? AddToToml(string original)
    {
        if (Regex.IsMatch(original, TomlEntryPattern, RegexOptions.Multiline))
            return null;

        string section =
            $"[mcp_servers.{RouterMcpConfig.ServerName}]\n" +
            $"command = {TomlBasicString(RouterMcpConfig.RouterPath)}\n";

        string separator = original.Length == 0 || original.EndsWith('\n') ? "\n" : "\n\n";
        return original + separator + section;
    }

    private const string TomlEntryPattern = """^\s*\[mcp_servers\.("?)rhino\1\]""";

    private static JsonNode StandardEntry() => new JsonObject
    {
        ["command"] = RouterMcpConfig.RouterPath,
    };

    private static JsonNode OpenCodeEntry() => new JsonObject
    {
        ["type"] = "local",
        ["command"] = new JsonArray(RouterMcpConfig.RouterPath),
        ["enabled"] = true,
    };

    // Same-directory temp then atomic move, so a tool reading the config concurrently never sees a
    // half-written file. A unique temp name avoids two Rhino instances colliding on the same write.
    private static void WriteAtomic(string path, string content)
    {
        string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        string temp = Path.Combine(directory, $".rhmcp-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    private static string ResolvePath(string relative)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.GetFullPath(Path.Combine(home, relative));
    }

    private static string TomlBasicString(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static JsonDocumentOptions LenientJson { get; } =
        new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    private static JsonSerializerOptions IndentedJson { get; } =
        new(McpSerializer.Options) { WriteIndented = true };

    private static void Log(string message)
    {
        try
        {
            RhinoApp.WriteLine($"[Rhino MCP] {message}");
        }
        catch
        {
        }
    }
}
