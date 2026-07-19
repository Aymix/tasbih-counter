using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace TasbihCounter;

/// <summary>One configured dhikr: which tap key drives it, its label and color.</summary>
public sealed class DhikrConfig
{
    public TapKey Key { get; set; }
    public bool Enabled { get; set; } = true;
    public string Label { get; set; } = "";
    public string ColorHex { get; set; } = "#66E0A3";

    [JsonIgnore]
    public Color Color => ConfigStore.ParseColor(ColorHex);
}

/// <summary>Everything the app persists. Counts are NOT stored (reset per v1).</summary>
public sealed class AppConfig
{
    public HudPosition Position { get; set; } = HudPosition.TopRight;
    public HudSize Size { get; set; } = HudSize.Medium;
    public int HoldMs { get; set; } = 900;
    public List<DhikrConfig> Adhkar { get; set; } = new();

    /// <summary>The default three adhkar (Subḥān Allāh, al-ḥamdu lillāh, Allāhu akbar).</summary>
    public static AppConfig Defaults() => new()
    {
        Adhkar =
        {
            new DhikrConfig { Key = TapKey.Alt,   Label = "سُبْحَانَ اللّٰه", ColorHex = "#66E0A3" }, // SubhanAllah
            new DhikrConfig { Key = TapKey.Ctrl,  Label = "اَلْحَمْدُ لِلّٰه", ColorHex = "#7EC8E3" }, // Alhamdulillah
            new DhikrConfig { Key = TapKey.Shift, Label = "اللّٰهُ أَكْبَر", ColorHex = "#F0C674" }, // Allahu akbar
        }
    };
}

/// <summary>Loads and saves <see cref="AppConfig"/> as JSON under %AppData%.</summary>
public static class ConfigStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TasbihCounter");
    private static readonly string FilePath = Path.Combine(Dir, "config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), Options);
                if (cfg is { Adhkar.Count: > 0 }) return cfg;
            }
        }
        catch
        {
            // Corrupt or unreadable config -> fall back to defaults.
        }
        return AppConfig.Defaults();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(config, Options));
    }

    /// <summary>Parse "#RRGGBB" (or "#AARRGGBB") to a Color; grey on failure.</summary>
    public static Color ParseColor(string hex)
    {
        try
        {
            if (ColorConverter.ConvertFromString(hex) is Color c) return c;
        }
        catch { }
        return Color.FromRgb(0x9A, 0xA0, 0xA6);
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
