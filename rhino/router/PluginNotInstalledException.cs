namespace RhMcp.Router;

// Thrown when a fresh Rhino launch would start a version whose Yak package (the
// MCP plugin) isn't installed: that Rhino comes up but never binds its port, so
// we fail fast with an actionable message instead of eating the full startup
// timeout. Detection is Yak-only and Release-only (see RhinoLocator.IsPluginInstalled);
// dev builds load the plugin from bin and never hit this.
public sealed class PluginNotInstalledException(string version, IReadOnlyList<string> versionsWithPlugin)
    : Exception(BuildMessage(version, versionsWithPlugin))
{
    private const string SetupDocsUrl = "https://mcneel.github.io/RhinoMCP/docs/getting-started/";

    public string Version { get; } = version;

    private static string BuildMessage(string version, IReadOnlyList<string> versionsWithPlugin)
    {
        string where = versionsWithPlugin.Count > 0
            ? $"It is installed for: {string.Join(", ", versionsWithPlugin)}. "
            : "No installed Rhino has it. ";
        return $"The Rhino MCP plugin is not installed for Rhino {version}. {where}" +
               $"Install it for the Rhino you want to use, then retry. Setup guide: {SetupDocsUrl}";
    }
}
