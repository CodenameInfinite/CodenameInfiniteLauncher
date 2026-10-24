using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using CodenameInfiniteLauncher.Models;

namespace CodenameInfiniteLauncher.Services;

/// <summary>
/// Optional, player-chosen addons — distinct from UpdateService's patches, which are
/// mandatory and always applied. Backed by the same addon folders the dashboard's
/// Downloads page already serves (GET /Downloads/AddonList, /Downloads/Addon?name=...);
/// this just gives the launcher a way to install/update/remove them without a browser.
/// </summary>
public class AddonService
{
    private readonly HttpClient _http;
    private readonly LauncherConfig _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AddonService(LauncherConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    private string AddOnsRoot => Path.Combine(_config.ClientPath, "Interface", "AddOns");

    public async Task<List<AddonEntry>> FetchAddonsAsync()
    {
        var url = $"{_config.DashboardBaseUrl.TrimEnd('/')}/Downloads/AddonList";
        var json = await _http.GetStringAsync(url);
        var parsed = JsonSerializer.Deserialize<AddonsResponse>(json, JsonOpts);
        return parsed?.Addons ?? new List<AddonEntry>();
    }

    /// <summary>Whether this addon has a folder under Interface\AddOns\ at all.</summary>
    public bool IsInstalled(AddonEntry addon) =>
        Directory.Exists(Path.Combine(AddOnsRoot, addon.Folder));

    /// <summary>
    /// The installed copy's own Version line, read straight from its .toc — not a hash, since
    /// the local folder was extracted from a zip and never carries the zip's own hash with it.
    /// Null if not installed or the .toc is missing/unreadable.
    /// </summary>
    public string? GetInstalledVersion(AddonEntry addon)
    {
        var tocPath = Path.Combine(AddOnsRoot, addon.Folder, addon.Folder + ".toc");
        if (!File.Exists(tocPath)) return null;

        foreach (var line in File.ReadLines(tocPath))
        {
            if (line.StartsWith("## Version:"))
                return line.Substring("## Version:".Length).Trim();
        }
        return null;
    }

    /// <summary>True if not installed, or installed under a different Version than the server lists.</summary>
    public bool HasUpdate(AddonEntry addon)
    {
        var installed = GetInstalledVersion(addon);
        return installed is null || !string.Equals(installed, addon.Version, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Downloads the addon's zip and extracts it into Interface\AddOns\. The zip's entries are
    /// already rooted under "&lt;Folder&gt;/" (see DownloadsController.BuildAddonZipBytes), so
    /// extracting straight into AddOnsRoot recreates AddOnsRoot\&lt;Folder&gt;\... correctly.
    /// Any existing copy of the folder is removed first so a version that dropped files doesn't
    /// leave the old ones behind.
    /// </summary>
    public async Task InstallAddonAsync(AddonEntry addon, IProgress<(int percent, string status)>? progress = null)
    {
        Directory.CreateDirectory(AddOnsRoot);

        progress?.Report((0, $"downloading {addon.Folder}"));

        var downloadUrl = $"{_config.DashboardBaseUrl.TrimEnd('/')}{addon.Url}";
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"{addon.Folder}-{Guid.NewGuid():N}.zip");

        try
        {
            using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using var httpStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write);

                long totalBytes = response.Content.Headers.ContentLength ?? addon.SizeBytes;
                long doneBytes = 0;
                var buffer = new byte[81920];
                int read;
                while ((read = await httpStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    doneBytes += read;
                    int pct = totalBytes <= 0 ? 0 : (int)Math.Clamp(doneBytes * 100 / totalBytes, 0, 100);
                    progress?.Report((pct, $"downloading {addon.Folder}"));
                }
            }

            progress?.Report((100, $"installing {addon.Folder}"));

            var installedDir = Path.Combine(AddOnsRoot, addon.Folder);
            if (Directory.Exists(installedDir))
                Directory.Delete(installedDir, recursive: true);

            ZipFile.ExtractToDirectory(tempZipPath, AddOnsRoot, overwriteFiles: true);
        }
        finally
        {
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);
        }
    }

    /// <summary>Removes the addon's folder entirely. Does not touch its SavedVariables —
    /// those live under WTF\Account\...\SavedVariables\ and are harmless left behind.</summary>
    public void UninstallAddon(AddonEntry addon)
    {
        var installedDir = Path.Combine(AddOnsRoot, addon.Folder);
        if (Directory.Exists(installedDir))
            Directory.Delete(installedDir, recursive: true);
    }
}
