using Eto.Drawing;
using Eto.Forms;
using RhinoCommand = Rhino.Commands.Command;

namespace RhMcp;

public class MCPConnectCommand : RhinoCommand
{
    public override string EnglishName => "MCPConnect";

    protected override string CommandContextHelpUrl => "https://mcneel.github.io/RhinoMCP";

    protected override Rhino.Commands.Result RunCommand(RhinoDoc doc, Rhino.Commands.RunMode mode)
    {
        ConnectDialog dialog = new();
        dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
        return Rhino.Commands.Result.Success;
    }
}

internal sealed class ConnectDialog : Dialog
{
    // Advanced-tab fields, keyed by their env override, so Refresh can rebuild the
    // env dict from whatever the user has typed.
    private readonly Dictionary<RouterEnvOverride, TextBox> _fields = [];

    private readonly TextArea _promptTextArea;
    private readonly TextArea _jsonTextArea;

    public ConnectDialog()
    {
        Title = "Connect Rhino to your AI Agent";

        Resizable = true;
        Padding = new Padding(12);
        Size = new Size(420, 340);

        _promptTextArea = new TextArea
        {
            ReadOnly = true,
            Wrap = true,
            Font = Fonts.Monospace(11),
        };

        _jsonTextArea = new TextArea
        {
            ReadOnly = true,
            Wrap = false,
            Font = Fonts.Monospace(11),
        };

        Label blurb = new()
        {
            Text = "Paste this prompt into your MCP-aware AI agent (e.g. Claude), it will handle the connection for you.",
            Wrap = WrapMode.Word,
        };

        TabControl tabs = new();
        TabPage promptTab = new() { Text = "Prompt", Content = _promptTextArea };
        TabPage jsonTab = new() { Text = "mcp.json", Content = _jsonTextArea };
        tabs.Pages.Add(promptTab);
        tabs.Pages.Add(jsonTab);
        tabs.Pages.Add(new TabPage { Text = "Advanced", Content = BuildAdvanced() });

        Button copyButton = new() { Text = "Copy" };
        copyButton.Click += (_, _) =>
        {
            TextArea active = tabs.SelectedPage == jsonTab ? _jsonTextArea : _promptTextArea;
            Clipboard.Instance.Text = active.Text;
            copyButton.Text = "Copied!";
        };

        Button closeButton = new() { Text = "Close" };
        closeButton.Click += (_, _) => Close();

        StackLayout buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            Items = { null, copyButton, closeButton },
        };

        Content = new TableLayout
        {
            Spacing = new Size(0, 8),
            Rows =
            {
                new TableRow(blurb),
                new TableRow(tabs) { ScaleHeight = true },
                new TableRow(buttons),
            },
        };

        DefaultButton = closeButton;
        AbortButton = closeButton;

        Refresh();
    }

    // One label + text field per override; editing any field rebuilds both tabs.
    private Control BuildAdvanced()
    {
        TableLayout table = new() { Spacing = new Size(8, 6), Padding = new Padding(0, 4) };

        foreach (RouterEnvOverride ov in RouterEnvOverride.Catalog)
        {
            TextBox field = new() { PlaceholderText = ov.Placeholder, ToolTip = ov.Help };
            field.TextChanged += (_, _) => Refresh();
            _fields[ov] = field;

            Label label = new() { Text = ov.Label, ToolTip = ov.Help, VerticalAlignment = VerticalAlignment.Center };
            table.Rows.Add(new TableRow(label, new TableCell(field, scaleWidth: true)));
        }

        table.Rows.Add(null); // soak up spare vertical space
        return new Scrollable { Content = table, Border = BorderType.None };
    }

    // Only fields the user changed from their default become env entries.
    private IReadOnlyDictionary<string, string> CurrentEnv()
    {
        Dictionary<string, string> env = [];
        foreach ((RouterEnvOverride ov, TextBox field) in _fields)
        {
            if (ov.IsSet(field.Text))
                env[ov.EnvVar] = field.Text.Trim();
        }
        return env;
    }

    private void Refresh()
    {
        IReadOnlyDictionary<string, string> env = CurrentEnv();
        _promptTextArea.Text = Prompt(env);
        _jsonTextArea.Text = RouterMcpConfig.BuildJson(env);
    }

    private static string Prompt(IReadOnlyDictionary<string, string> env) =>
$@"Install the Rhino MCP server. The entry is:

{RouterMcpConfig.EntryFragment(env)}

Then tell the user to reload";
}
