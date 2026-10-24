using System.Text.Json.Serialization;

namespace CodenameInfiniteLauncher.Models;

/// <summary>Minimal shape of GitHub's GET /repos/{owner}/{repo}/releases/latest response.</summary>
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    /// <summary>The asset's API url — the only way to fetch a private repo's asset bytes with a
    /// token (GET + "Accept: application/octet-stream" + "Authorization"). BrowserDownloadUrl
    /// only works for a logged-in browser session with repo access, never for a token.</summary>
    [JsonPropertyName("url")]
    public string ApiUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
