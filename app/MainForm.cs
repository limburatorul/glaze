using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Glaze;

public sealed class MainForm : Form
{
    private const string Home = "https://www.youtube.com";

    // "Info sub player" off → the player fills everything under the top bar.
    // YouTube sizes this chain of containers from JS, so every level is forced to 100%.
    private const string FullPlayerCss = """
        ytd-watch-flexy #columns { display: none !important; }
        ytd-watch-flexy #full-bleed-container { height: calc(100vh - 56px) !important; max-height: none !important; }
        ytd-watch-flexy #player-container-outer, ytd-watch-flexy #player-container-inner,
        ytd-watch-flexy #player-container, ytd-watch-flexy #player, ytd-watch-flexy #movie_player,
        ytd-watch-flexy .html5-video-container { height: 100% !important; max-height: none !important; padding-top: 0 !important; }
        ytd-watch-flexy video { width: 100% !important; height: 100% !important; left: 0 !important; top: 0 !important; object-fit: contain !important; }
        """;

    private readonly WebView2 _web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(0x0e, 0x0e, 0x12) };
    private CoreWebView2Environment? _env;
    private SettingsForm? _settings;
    private string _frameJs = "", _pageJs = "", _baseCss = "";
    private (FormWindowState State, Rectangle Bounds)? _beforeFullScreen;

    public MainForm()
    {
        Text = "Glaze";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        Size = new Size(1280, 800);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(0x0e, 0x0e, 0x12);
        TopMost = Settings.Bool("onTop");
        Controls.Add(_web);
        // The WinForms control turns WebView2's accelerator keys into KeyDown; Handled keeps them from the page.
        _web.KeyDown += OnKey;
        Load += async (_, _) => await Start();
        Resize += (_, _) => Run($"window.__glazeFrame && window.__glazeFrame.maximized({Js(WindowState == FormWindowState.Maximized)})");
    }

    private async Task Start()
    {
        try
        {
            _env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Resources.DataDir, "WebView2"),
                new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true });
            await _web.EnsureCoreWebView2Async(_env);
        }
        catch (WebView2RuntimeNotFoundException)
        {
            // Part of Windows 11, but it can be removed; without it there is nothing to show.
            MessageBox.Show("Glaze needs the Microsoft Edge WebView2 Runtime, which is not installed.\n\n" +
                "Download it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and start Glaze again.",
                "Glaze", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
            return;
        }

        var core = _web.CoreWebView2;
        // Browser shortcuts (print, find, zoom, reload...) are off; the ones Glaze has are handled below.
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        // `app-region: drag` in style.css is what makes YouTube's top bar move the window.
        core.Settings.IsNonClientRegionSupportEnabled = true;
        // YouTube follows the OS theme by default → force dark. Cookie "wide=1" = theater mode.
        core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
        var wide = core.CookieManager.CreateCookie("wide", "1", ".youtube.com", "/");
        wide.Expires = DateTime.UtcNow.AddYears(5);
        core.CookieManager.AddOrUpdateCookie(wide);

        await LoadExtensions(core.Profile);

        _frameJs = Resources.Text("frame.js").Replace("%ICON%",
            "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(Resources.Web, "icon.png"))));
        _pageJs = Resources.Text("page.js");
        _baseCss = Resources.Text("style.css") + Wallpaper();

        core.DOMContentLoaded += async (_, _) => await Inject();
        core.WebMessageReceived += OnMessage;
        // Links that try to open a new window stay in this one. Anything that isn't a web page - SponsorBlock
        // opens its welcome page as a tab on first run - has no tab to go to and is dropped, as in Electron.
        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (e.Uri.StartsWith("https://") || e.Uri.StartsWith("http://")) core.Navigate(e.Uri);
        };
        core.ContainsFullScreenElementChanged += (_, _) => SetFullScreen(core.ContainsFullScreenElement);
        core.DocumentTitleChanged += (_, _) => Text = core.DocumentTitle is { Length: > 0 } t ? t : "Glaze";
        core.Navigate(Home);

        Updater.StartChecks(this);
    }

    /// <summary>Every unpacked extension bundled in the exe (SponsorBlock). Installed into the profile
    /// once; after that WebView2 loads it itself from the same folder, which each update rewrites.</summary>
    private static async Task LoadExtensions(CoreWebView2Profile profile)
    {
        if (!Directory.Exists(Resources.Extensions)) return;
        var installed = await profile.GetBrowserExtensionsAsync();
        foreach (var dir in Directory.GetDirectories(Resources.Extensions))
        {
            if (!File.Exists(Path.Combine(dir, "manifest.json"))) continue;
            var name = Path.GetFileName(dir);
            if (installed.Any(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase))) continue;
            try { await profile.AddBrowserExtensionAsync(dir); }
            catch (Exception ex) when (ex is COMException or ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"Extension {name} failed to load: {ex.Message}");
            }
        }
    }

    /// <summary>The wallpaper behind the glass: a background.jpg / .png / .webp next to Glaze.exe if there
    /// is one, otherwise the one Glaze ships with.</summary>
    private static string Wallpaper()
    {
        var file = new[] { "jpg", "png", "webp" }.Select(e => Path.Combine(AppContext.BaseDirectory, $"background.{e}"))
            .Append(Path.Combine(Resources.Web, "background.png")).FirstOrDefault(File.Exists);
        if (file == null) return "";
        var type = Path.GetExtension(file)[1..].Replace("jpg", "jpeg");
        return $"html{{--wallpaper:url(\"data:image/{type};base64,{Convert.ToBase64String(File.ReadAllBytes(file))}\")}}";
    }

    private static string OptionsCss() => string.Join("\n", new[]
    {
        Settings.Bool("related") ? null : "#related { display: none !important; }",
        Settings.Bool("comments") ? null : "ytd-comments#comments { display: none !important; }",
        Settings.Bool("info") ? null : FullPlayerCss,
        Settings.Bool("ambient") ? "#yp-ambient { display: block !important; }" : null,
        FormattableString.Invariant($"html {{ --glass-blur-scale: {Settings.Number("blur")}; --yp-card: {Settings.Number("card")}px; }}"),
    }.Where(s => s != null));

    private static bool OnYouTube(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && u.Host == "www.youtube.com";

    private async Task Inject()
    {
        // The window's buttons on every page; on anything but YouTube (Google's sign-in) a Glaze title bar too.
        var core = _web.CoreWebView2;
        await core.ExecuteScriptAsync(_frameJs);
        await core.ExecuteScriptAsync($"window.__glazeFrame.maximized({Js(WindowState == FormWindowState.Maximized)}); " +
            $"window.__glazeFrame.fullscreen({Js(_beforeFullScreen != null)})");
        // YouTube's restyling only on YouTube: the sign-in pages keep their own look.
        if (!OnYouTube(_web.Source.ToString())) return;
        await core.ExecuteScriptAsync(_pageJs);
        await core.ExecuteScriptAsync($"window.__glaze.css({Js(_baseCss)}, {Js(OptionsCss())})");
    }

    private void Run(string script)
    {
        if (_web.CoreWebView2 != null) _ = _web.CoreWebView2.ExecuteScriptAsync(script);
    }

    private static string Js<T>(T value) => JsonSerializer.Serialize(value);

    /// <summary>Called by the settings window after it stored a change.</summary>
    public void SettingChanged(string key)
    {
        if (key == "onTop") TopMost = Settings.Bool("onTop");
        else Run($"window.__glaze && window.__glaze.css(null, {Js(OptionsCss())})");
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(e.WebMessageAsJson); } catch (JsonException) { return; }
        switch (msg?["type"]?.GetValueKind() == JsonValueKind.String ? (string?)msg["type"] : null)
        {
            // Only youtube.com has the settings button.
            case "openSettings" when OnYouTube(e.Source):
                ShowSettings();
                break;
            // The window's own buttons are on every page (frame.js), Google's sign-in included. The most any
            // page can do with these is minimise, maximise or close the window.
            case "window":
                switch (msg!["action"]?.GetValueKind() == JsonValueKind.String ? (string?)msg["action"] : null)
                {
                    case "minimize": WindowState = FormWindowState.Minimized; break;
                    case "maximize": WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; break;
                    case "close": Close(); break;
                }
                break;
        }
    }

    public void ShowSettings()
    {
        if (_env == null) return;
        if (_settings != null) { _settings.Activate(); return; }
        _settings = new SettingsForm(this, _env);
        _settings.FormClosed += (_, _) => _settings = null;
        _settings.Show(this);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (_web.CoreWebView2 == null) return;
        Action? action = e.KeyData switch
        {
            Keys.Control | Keys.Oemcomma => ShowSettings,
            Keys.Control | Keys.T => () => { Settings.Set("onTop", JsonValue.Create(!Settings.Bool("onTop"))); SettingChanged("onTop"); },
            Keys.Alt | Keys.Left => () => { if (_web.CanGoBack) _web.GoBack(); },
            Keys.Alt | Keys.Right => () => { if (_web.CanGoForward) _web.GoForward(); },
            Keys.F5 => _web.Reload,
            Keys.F11 => () =>
            {
                // The player's own full screen is left through the page, which then tells us.
                if (_web.CoreWebView2.ContainsFullScreenElement) Run("document.exitFullscreen()");
                else SetFullScreen(_beforeFullScreen == null);
            },
            Keys.Control | Keys.Shift | Keys.I => _web.CoreWebView2.OpenDevToolsWindow,
            _ => null,
        };
        if (action == null) return;
        e.Handled = true;
        // Not run here: WebView2 waits for this handler before it goes on, and an action that talks back to
        // it (always on top, full screen, reload) would then wait on WebView2 in turn. Queued instead.
        BeginInvoke(action);
    }

    private void SetFullScreen(bool on)
    {
        if (on == (_beforeFullScreen != null)) return;
        if (on)
        {
            _beforeFullScreen = (WindowState, WindowState == FormWindowState.Normal ? Bounds : RestoreBounds);
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            var (state, bounds) = _beforeFullScreen!.Value;
            _beforeFullScreen = null;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = bounds;
            WindowState = state;
        }
        Run($"window.__glazeFrame && window.__glazeFrame.fullscreen({Js(on)})");
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.DarkTitleBar(Handle);
    }

    /// <summary>
    /// Google rotates its sign-in cookies while YouTube is open, and WebView2 writes cookies to disk only
    /// now and then. Left open when Windows shuts down, Glaze was killed with the fresh cookies still in
    /// memory, and the next start opened YouTube signed out. So the close waits for WebView2's browser
    /// process to exit, which is when it has saved everything.
    /// </summary>
    private bool _flushed;
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (_flushed || e.Cancel || _env == null) return;
        e.Cancel = true;
        var exited = new TaskCompletionSource();
        _env.BrowserProcessExited += (_, _) => exited.TrySetResult();
        _settings?.Close();
        _web.Dispose();
        await Task.WhenAny(exited.Task, Task.Delay(5000));
        _flushed = true;
        Close();
    }

    /// <summary>
    /// No title bar, but the rest of a normal window: the caption is cut off the top of the frame while
    /// the side and bottom borders stay, so resizing, snapping and the shadow keep working. YouTube's
    /// top bar takes the caption's place (page.js draws the buttons, style.css the drag region).
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_NCCALCSIZE && m.WParam != 0 && FormBorderStyle == FormBorderStyle.Sizable)
        {
            var top = Marshal.ReadInt32(m.LParam, 4); // NCCALCSIZE_PARAMS.rgrc[0].top
            base.WndProc(ref m);
            // Maximized, Windows pushes the frame past the screen edge; keep the content on screen.
            var inset = WindowState == FormWindowState.Maximized
                ? Native.GetSystemMetrics(Native.SM_CYFRAME) + Native.GetSystemMetrics(Native.SM_CXPADDEDBORDER)
                : 0;
            Marshal.WriteInt32(m.LParam, 4, top + inset);
            m.Result = 0;
            return;
        }
        base.WndProc(ref m);
    }
}
