using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Glaze;

/// <summary>settings.html in a small acrylic window, the way the Electron build had it.</summary>
public sealed class SettingsForm : Form
{
    private const string Host = "glaze.settings";
    private const string Page = $"https://{Host}/settings.html";

    // What preload.js gave settings.html in the Electron build, so the page did not have to change.
    private const string Bridge = """
        window.ytPlayer = {
          getSettings: () => new Promise(resolve => {
            const reply = e => {
              if (e.data && e.data.type === 'settings') { window.chrome.webview.removeEventListener('message', reply); resolve(e.data.settings); }
            };
            window.chrome.webview.addEventListener('message', reply);
            window.chrome.webview.postMessage({ type: 'get' });
          }),
          setSetting: (key, value) => window.chrome.webview.postMessage({ type: 'set', key, value }),
        };
        """;

    // The footer links open in the user's browser / mail client, never inside this window.
    private static readonly Regex External = new(@"^(https://protagonistlabs\.app/|mailto:feedback@protagonistlabs\.app\?)");

    private readonly MainForm _main;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Transparent };

    public SettingsForm(MainForm main, CoreWebView2Environment env)
    {
        _main = main;
        Text = "Glaze settings";
        Icon = main.Icon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 820);
        // Black is what DWM treats as see-through once the frame covers the window (Native.Acrylic).
        BackColor = Color.Black;
        Controls.Add(_web);
        Load += async (_, _) => await Start(env);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.Acrylic(Handle);
    }

    private async Task Start(CoreWebView2Environment env)
    {
        await _web.EnsureCoreWebView2Async(env);
        var core = _web.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.SetVirtualHostNameToFolderMapping(Host, Resources.Web, CoreWebView2HostResourceAccessKind.DenyCors);
        await core.AddScriptToExecuteOnDocumentCreatedAsync(Bridge);
        core.NavigationStarting += (_, e) =>
        {
            if (e.Uri == Page) return;
            e.Cancel = true;
            if (External.IsMatch(e.Uri)) Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
        };
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.WebMessageReceived += OnMessage;
        core.Navigate(Page);
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (e.Source != Page) return;
        JsonNode? msg;
        try { msg = JsonNode.Parse(e.WebMessageAsJson); } catch (JsonException) { return; }
        var type = msg?["type"]?.GetValueKind() == JsonValueKind.String ? (string?)msg["type"] : null;
        if (type == "get")
            _web.CoreWebView2.PostWebMessageAsJson($$"""{"type":"settings","settings":{{Settings.Json}}}""");
        else if (type == "set" && msg!["key"]?.GetValueKind() == JsonValueKind.String)
        {
            var key = (string)msg["key"]!;
            if (Settings.Set(key, msg["value"])) _main.SettingChanged(key);
        }
        else if (type == "pickBackground") PickBackground();
        else if (type == "resetBackground")
        {
            RemovePickedBackground();
            _main.BackgroundChanged();
        }
    }

    private void PickBackground()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a background image",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var type = Path.GetExtension(dialog.FileName).TrimStart('.').ToLowerInvariant().Replace("jpeg", "jpg");
        if (!MainForm.BackgroundTypes.Contains(type)) return;
        try
        {
            RemovePickedBackground();
            Directory.CreateDirectory(MainForm.PickedBackgroundDir);
            File.Copy(dialog.FileName, Path.Combine(MainForm.PickedBackgroundDir, $"background.{type}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Glaze couldn't use that image.\n\n{ex.Message}", "Glaze", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _main.BackgroundChanged();
    }

    // A copy is kept, not the path: the picked file can be moved or deleted later.
    private static void RemovePickedBackground()
    {
        foreach (var type in MainForm.BackgroundTypes)
            File.Delete(Path.Combine(MainForm.PickedBackgroundDir, $"background.{type}"));
    }
}
