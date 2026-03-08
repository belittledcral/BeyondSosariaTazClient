using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BeyondSosariaLauncher;

public class GitHubAsset
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}

public class GitHubRelease
{
    [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

[JsonSerializable(typeof(LauncherConfig))]
[JsonSerializable(typeof(ServerEntry))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(List<ServerEntry>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<GitHubAsset>))]
internal partial class LauncherJsonContext : JsonSerializerContext { }
