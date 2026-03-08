using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BeyondSosariaLauncher;

public class ServerEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 2593;
}

public class LauncherConfig
{
    [JsonPropertyName("uoDataPath")]
    public string UoDataPath { get; set; } = "";

    [JsonPropertyName("lastUsername")]
    public string LastUsername { get; set; } = "";

    [JsonPropertyName("savedPassword")]
    public string SavedPassword { get; set; } = "";

    [JsonPropertyName("rememberPassword")]
    public bool RememberPassword { get; set; } = false;

    [JsonPropertyName("servers")]
    public List<ServerEntry> Servers { get; set; } = new()
    {
        new ServerEntry { Name = "Beyond Sosaria", Host = "play.beyondsosaria.com", Port = 2593 }
    };

    [JsonPropertyName("lastServerIndex")]
    public int LastServerIndex { get; set; } = 0;

    [JsonPropertyName("enabledPlugins")]
    public List<string> EnabledPlugins { get; set; } = new() { "RazorEnhanced.dll" };

    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "launcher-config.json");

    public static LauncherConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize(json, LauncherJsonContext.Default.LauncherConfig);
                if (cfg != null)
                {
                    // Ensure the default Beyond Sosaria server is always first
                    if (cfg.Servers.Count == 0 || cfg.Servers[0].Name != "Beyond Sosaria")
                        cfg.Servers.Insert(0, new ServerEntry { Name = "Beyond Sosaria", Host = "play.beyondsosaria.com", Port = 2593 });
                    return cfg;
                }
            }
        }
        catch { /* fall through to default */ }

        return new LauncherConfig();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, LauncherJsonContext.Default.LauncherConfig);
        File.WriteAllText(ConfigPath, json);
    }
}
