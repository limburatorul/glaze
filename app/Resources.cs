using System.Reflection;

namespace Glaze;

/// <summary>
/// The page files and the bundled extensions live inside the exe (see Glaze.csproj), so the portable
/// build is one file. WebView2 needs real folders for an extension and for the settings page, so they
/// are unpacked once per version. The folder itself never moves: WebView2 remembers an extension by the
/// folder it was added from, and reloads it from there on every start.
/// </summary>
public static class Resources
{
    public static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glaze");

    private static readonly string Dir = Path.Combine(DataDir, "app");
    public static string Web => Path.Combine(Dir, "web");
    public static string Extensions => Path.Combine(Dir, "ext");

    public static string Text(string name) => File.ReadAllText(Path.Combine(Web, name));

    public static void Extract()
    {
        var marker = Path.Combine(Dir, ".version");
        var version = Updater.Current.ToString();
        if (File.Exists(marker) && File.ReadAllText(marker) == version) return;
        // Another version's files (or an unpack cut short): start clean, so nothing stale is left behind.
        if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith("web/") && !name.StartsWith("ext/")) continue;
            var path = Path.Combine(Dir, name.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var source = assembly.GetManifestResourceStream(name)!;
            using var file = File.Create(path);
            source.CopyTo(file);
        }
        // Written last: an unpack cut short by a crash is redone on the next start rather than trusted.
        File.WriteAllText(marker, version);
    }
}
