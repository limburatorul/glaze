using System.Text.Json;
using System.Text.Json.Nodes;

namespace Glaze;

/// <summary>
/// The studio's shared folder, %APPDATA%\ProtagonistLabs (contract: Proiecte/Ecosistem.md in the vault).
/// Every Protagonist Labs app writes a card about itself into apps\ at every start; the others read the
/// cards to know what is installed and how to call it. Nothing leaves the PC.
/// </summary>
internal static class Ecosystem
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProtagonistLabs");
    private const string Site = "https://protagonistlabs.app";
    private const string Utm = "utm_source=glaze&utm_medium=app&utm_campaign=works-with";

    /// <summary>Glaze's card. Off the UI thread and never fatal: a locked or missing folder only means
    /// the other apps don't see Glaze this time.</summary>
    public static void WriteCard() => Task.Run(() =>
    {
        try
        {
            var file = Path.Combine(Root, "apps", "glaze.json");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var card = new JsonObject
            {
                ["schema"] = 1,
                ["updated"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["id"] = "glaze",
                ["name"] = "Glaze",
                ["version"] = Updater.Current.ToString(),
                ["exe"] = Environment.ProcessPath,
                ["edition"] = "free",
                ["paid"] = false,
                ["page"] = $"{Site}/glaze/",
                // A youtube.com address on the command line opens it; an open Glaze navigates instead.
                ["commands"] = new JsonObject { ["open"] = new JsonArray("{url}") },
            };
            File.WriteAllText(file + ".tmp", card.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(file + ".tmp", file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Card not written: {ex.Message}");
        }
    });

    /// <summary>Another app's card, or null when it is missing, unreadable, another schema, or its exe is
    /// gone (uninstalled). Written by another program, so trusted for nothing.</summary>
    private static JsonObject? Card(string id)
    {
        try
        {
            if (JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "apps", $"{id}.json"))) is not JsonObject card) return null;
            if (card["schema"]?.GetValueKind() != JsonValueKind.Number || (int)card["schema"]! != 1) return null;
            var exe = card["exe"]?.GetValueKind() == JsonValueKind.String ? (string?)card["exe"] : null;
            return exe != null && File.Exists(exe) ? card : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    private static readonly (string Id, string Name, string Code)[] Paid =
    [
        ("shelf", "Shelf", "SECOND30SHELF"), ("stash", "Stash", "SECOND30"), ("frostpane", "Frostpane", "SECOND30"),
        ("reel", "Reel", "SECOND30"), ("planner", "Planner", "SECOND30"), ("reminder", "Reminder", "SECOND30"),
    ];

    /// <summary>The settings page's "Works with": the one sister that adds something to Glaze (Reel),
    /// installed or not, and one quiet discount line, only once something was bought.</summary>
    public static JsonObject WorksWith()
    {
        var sisters = new JsonArray(Card("reel") != null
            ? new JsonObject { ["id"] = "reel", ["name"] = "Reel", ["text"] = "a film's Trailers on YouTube open here" }
            : new JsonObject { ["id"] = "reel", ["name"] = "Reel", ["text"] = "would open a film's trailers here", ["url"] = $"{Site}/reel/?{Utm}" });
        var cards = Paid.Select(p => (p, Card: Card(p.Id))).ToList();
        var bought = cards.Any(c => c.Card?["paid"]?.GetValueKind() == JsonValueKind.True
            && c.Card["edition"]?.GetValueKind() == JsonValueKind.String && (string?)c.Card["edition"] == "full");
        var offer = new JsonArray();
        if (bought)
            foreach (var (p, _) in cards.Where(c => c.Card == null).Take(4))
                offer.Add(new JsonObject { ["name"] = p.Name, ["url"] = $"{Site}/{p.Id}/?code={p.Code}&{Utm}" });
        return new JsonObject { ["type"] = "worksWith", ["sisters"] = sisters, ["offer"] = offer };
    }

    /// <summary>An installed sister's name in Works with starts it. The exe comes from its card again
    /// here, never from the page.</summary>
    public static void OpenSister(string id)
    {
        if (id != "reel" || Card(id)?["exe"] is not JsonValue exe) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo((string)exe!) { UseShellExecute = false }); }
        catch (System.ComponentModel.Win32Exception ex) { System.Diagnostics.Debug.WriteLine($"{id} did not start: {ex.Message}"); }
    }
}
