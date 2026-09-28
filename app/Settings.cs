using System.Text.Json;
using System.Text.Json.Nodes;

namespace Glaze;

/// <summary>
/// The switches and sliders in the settings window. Defaults double as the schema: YouTube's page can
/// post messages to the app too, so every write is checked against them. Kept in the same file the
/// Electron build used, so settings survive the move.
/// </summary>
public static class Settings
{
    private static readonly string File = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Glaze", "settings.json");

    private static readonly Dictionary<string, JsonValue> Defaults = new()
    {
        ["related"] = JsonValue.Create(false), ["comments"] = JsonValue.Create(false), ["info"] = JsonValue.Create(false),
        ["ambient"] = JsonValue.Create(true), ["blur"] = JsonValue.Create(1.0), ["card"] = JsonValue.Create(300.0),
        ["onTop"] = JsonValue.Create(false),
    };
    private static readonly Dictionary<string, (double Min, double Max)> Limits = new() { ["blur"] = (0, 2), ["card"] = (180, 600) };

    private static readonly JsonObject Values = Load();

    private static JsonObject Load()
    {
        var values = new JsonObject();
        foreach (var (key, value) in Defaults) values[key] = value.DeepClone();
        try
        {
            if (JsonNode.Parse(System.IO.File.ReadAllText(File)) is JsonObject saved)
                foreach (var (key, value) in saved)
                    if (value is JsonValue v && Accepts(key, v)) values[key] = Clamp(key, v);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Missing on first run; unreadable means start from the defaults rather than not start.
        }
        return values;
    }

    public static string Json => Values.ToJsonString();
    public static bool Bool(string key) => Values[key]!.GetValue<bool>();
    public static double Number(string key) => Values[key]!.GetValue<double>();

    /// <summary>Stores a value if it is a known key of the right type. Returns false for anything else.</summary>
    public static bool Set(string key, JsonNode? value)
    {
        if (value is not JsonValue v || !Accepts(key, v)) return false;
        Values[key] = Clamp(key, v);
        Directory.CreateDirectory(Path.GetDirectoryName(File)!);
        System.IO.File.WriteAllText(File, Values.ToJsonString());
        return true;
    }

    private static bool Accepts(string key, JsonValue v) =>
        Defaults.TryGetValue(key, out var d) && v.GetValueKind() switch
        {
            JsonValueKind.True or JsonValueKind.False => d.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
            JsonValueKind.Number => d.GetValueKind() == JsonValueKind.Number,
            _ => false,
        };

    private static JsonValue Clamp(string key, JsonValue v) =>
        Limits.TryGetValue(key, out var l) ? JsonValue.Create(Math.Clamp(v.GetValue<double>(), l.Min, l.Max)) : (JsonValue)v.DeepClone();
}
