using System.Text.Json;
using System.Text.Json.Serialization;

namespace SC2Uncensor.Core;

public sealed class AppConfig
{
    [JsonPropertyName("beep")] public bool Beep { get; set; } = true;
    [JsonPropertyName("log")] public bool Log { get; set; } = true;
    [JsonPropertyName("forwardKeys")] public bool ForwardKeys { get; set; }
    [JsonPropertyName("verifyClickMs")] public int VerifyClickMs { get; set; } = 300;
    [JsonPropertyName("checkVersion")] public bool CheckVersion { get; set; } = true;
    [JsonPropertyName("screenshot")] public string Screenshot { get; set; } = "auto";
    [JsonPropertyName("hotkeys")] public Dictionary<string, string> Hotkeys { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["master"] = "Ctrl+Alt+1",
        ["uncensor"] = "F8",
        ["uncensorAuto"] = "F9",
        ["clickToggle"] = "F11",
        ["status"] = "Ctrl+Alt+2",
        ["restore"] = "Ctrl+Alt+3",
        ["restoreAuto"] = "Ctrl+Alt+4",
    };

    public int SafeVerifyClickMs => Math.Max(50, VerifyClickMs);

    public static AppConfig Load(string? path = null)
    {
        path ??= AppPaths.Config;
        if (!File.Exists(path)) return new AppConfig();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
