using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Glaze;

// Self-update, the way Sysoptimizer and File Labs do it (vault: File Labs/Release si update.md):
//   • check 3 s after launch and every 10 minutes — the app stays open for a while, and a release
//     published in the morning would otherwise be invisible until a restart;
//   • "Later" silences that one version for this session only; it is deliberately not persisted;
//   • updating downloads the release's asset, checks its size and SHA-256 against what GitHub published
//     for it, then hands off to a detached one-shot .cmd and quits: nothing in this process can outlive
//     the file that is about to replace it. The script waits for this PID, puts the new version in place
//     and starts Glaze again.
// Two kinds of copy, two assets: the installed one runs the new installer silently; the portable one
// (no uninstaller next to it) swaps its own exe for the new portable exe.
public static class Updater
{
    public const string Repo = "limburatorul/glaze";

    public record Release(Version Version, string Tag, string Notes, string AssetUrl, long AssetSize, string? Sha256);

    public static Version Current => typeof(Updater).Assembly.GetName().Version is { } v ? new Version(v.Major, v.Minor, v.Build) : new Version(0, 0, 0);

    public static bool Portable => !File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));
    // The names the Electron build used, kept because the site's download links are built from them.
    private static bool IsOurAsset(string name) => Portable
        ? name.EndsWith("-portable.exe", StringComparison.OrdinalIgnoreCase)
        : name.StartsWith("Glaze-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    private static string? _dismissed; // tag the user said "Later" to
    private static bool _prompting;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    public static void StartChecks(Form owner)
    {
        var first = new System.Windows.Forms.Timer { Interval = 3000 };
        first.Tick += async (_, _) => { first.Dispose(); await CheckAndPrompt(owner); };
        first.Start();
        var every = new System.Windows.Forms.Timer { Interval = 10 * 60 * 1000 };
        every.Tick += async (_, _) => await CheckAndPrompt(owner);
        every.Start();
    }

    private static async Task CheckAndPrompt(Form owner)
    {
        if (_prompting) return;
        var release = await Latest();
        if (release == null || release.Version <= Current || release.Tag == _dismissed) return;
        _prompting = true;
        try { UpdatePrompt.Show(owner, release); }
        finally { _dismissed = release.Tag; _prompting = false; }
    }

    /// <summary>The latest release, when it carries the asset this copy updates from.</summary>
    public static async Task<Release?> Latest()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
            request.Headers.UserAgent.ParseAdd("Glaze");
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;
            var asset = root.GetProperty("assets").EnumerateArray()
                .FirstOrDefault(a => IsOurAsset(a.GetProperty("name").GetString() ?? ""));
            if (asset.ValueKind != JsonValueKind.Object) return null;
            // GitHub publishes each asset's SHA-256 as "sha256:<hex>".
            var digest = asset.TryGetProperty("digest", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
            return new Release(version, tag, root.GetProperty("body").GetString() ?? "",
                asset.GetProperty("browser_download_url").GetString()!, asset.GetProperty("size").GetInt64(),
                digest?.StartsWith("sha256:") == true ? digest[7..] : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null; // offline, rate-limited, or a release without our asset: nothing to say
        }
    }

    /// <summary>Downloads the asset, reporting 0..1 progress, and verifies it. Returns its path.</summary>
    public static async Task<string> Download(Release release, IProgress<double> progress, CancellationToken token)
    {
        var path = Path.Combine(Path.GetTempPath(), Portable ? $"Glaze-{release.Version}-portable.exe" : $"Glaze-Setup-{release.Version}.exe");
        using (var response = await Http.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead, token))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? release.AssetSize;
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var file = File.Create(path);
            var buffer = new byte[128 * 1024];
            long done = 0;
            int n;
            while ((n = await source.ReadAsync(buffer, token)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, n), token);
                done += n;
                progress.Report(total > 0 ? (double)done / total : 0);
            }
        }
        // The size catches a cut-off download; the checksum catches everything else. This file is about
        // to be run, so one that differs from what was published never is.
        if (new FileInfo(path).Length != release.AssetSize)
        {
            File.Delete(path);
            throw new IOException("The download was cut short. Try again.");
        }
        if (release.Sha256 != null)
        {
            string actual;
            await using (var file = File.OpenRead(path)) actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
            if (!actual.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(path);
                throw new IOException("The download doesn't match the checksum GitHub published for it.");
            }
        }
        return path;
    }

    /// <summary>Hands off to a detached script; the caller then quits so the new version can take our place.</summary>
    public static void InstallAndRestart(string download)
    {
        var exe = Environment.ProcessPath!;
        var pid = Environment.ProcessId;
        var apply = Portable
            // move retries: Windows can hold the exe for a moment after the process is gone.
            ? $"""
              :move
              move /y "{download}" "{exe}" >nul || (timeout /t 1 /nobreak >nul & goto move)
              """
            : $"\"{download}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
        var script = Path.Combine(Path.GetTempPath(), $"glaze-update-{pid}.cmd");
        File.WriteAllText(script, $"""
            @echo off
            :wait
            tasklist /FI "PID eq {pid}" | find "{pid}" >nul && (timeout /t 1 /nobreak >nul & goto wait)
            {apply}
            start "" "{exe}"
            del "%~f0"
            """);
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false });
    }
}

/// <summary>"Glaze x.y.z is available" as a native task dialog: Update downloads with a progress bar,
/// then installs and restarts; Later closes it.</summary>
internal static class UpdatePrompt
{
    public static void Show(Form owner, Updater.Release release)
    {
        var update = new TaskDialogButton("Update") { AllowCloseDialog = false };
        var later = new TaskDialogButton("Later");
        var notes = release.Notes.Trim();
        var ask = new TaskDialogPage
        {
            Caption = "Glaze update",
            Heading = $"Glaze {release.Version} is available",
            Text = $"You have {Updater.Current}." + (notes.Length > 0 ? "\n\n" + notes : ""),
            Icon = TaskDialogIcon.Information,
            Buttons = { update, later },
            DefaultButton = update,
        };

        var cancel = new CancellationTokenSource();
        var bar = new TaskDialogProgressBar { Minimum = 0, Maximum = 100 };
        var stop = new TaskDialogButton("Cancel");
        var working = new TaskDialogPage
        {
            Caption = "Glaze update",
            Heading = $"Downloading Glaze {release.Version}…",
            Text = Updater.Portable ? "Glaze will restart when it's done." : "The installer will run and Glaze will restart.",
            ProgressBar = bar,
            Buttons = { stop },
        };
        stop.Click += (_, _) => cancel.Cancel();

        update.Click += (_, _) => ask.Navigate(working);
        working.Created += async (_, _) =>
        {
            try
            {
                var file = await Updater.Download(release, new Progress<double>(p => bar.Value = (int)(p * 100)), cancel.Token);
                Updater.InstallAndRestart(file);
                Application.Exit();
            }
            catch (OperationCanceledException)
            {
                // Cancel was pressed; the dialog is already closing.
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                working.Navigate(new TaskDialogPage
                {
                    Caption = "Glaze update",
                    Heading = "The update didn't download",
                    Text = ex.Message,
                    Icon = TaskDialogIcon.Warning,
                    Buttons = { TaskDialogButton.Close },
                });
            }
        };

        TaskDialog.ShowDialog(owner, ask);
        cancel.Cancel();
    }
}
