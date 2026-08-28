using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using CodenameInfiniteLauncher.Models;

namespace CodenameInfiniteLauncher.Services;

public class UpdateService
{
    private readonly HttpClient _http;
    private readonly LauncherConfig _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public UpdateService(LauncherConfig config)
    {
        _config = config;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<Manifest> FetchManifestAsync()
    {
        var url = $"{_config.DashboardBaseUrl.TrimEnd('/')}/Launcher/Manifest";
        var json = await _http.GetStringAsync(url);
        return JsonSerializer.Deserialize<Manifest>(json, JsonOpts)
            ?? throw new InvalidOperationException("Manifest response was empty.");
    }

    /// <summary>Returns the subset of patches whose local file is missing or hash-mismatched.</summary>
    public List<PatchEntry> GetOutdatedPatches(Manifest manifest)
    {
        var dataDir = Path.Combine(_config.ClientPath, "Data");
        var outdated = new List<PatchEntry>();

        foreach (var patch in manifest.Patches)
        {
            var localPath = Path.Combine(dataDir, patch.FileName);
            if (!File.Exists(localPath))
            {
                outdated.Add(patch);
                continue;
            }

            var localHash = ComputeSha256(localPath);
            if (!string.Equals(localHash, patch.Sha256, StringComparison.OrdinalIgnoreCase))
                outdated.Add(patch);
        }

        return outdated;
    }

    /// <summary>Downloads each outdated patch into Data\, reporting 0-100 overall progress.</summary>
    public async Task DownloadPatchesAsync(List<PatchEntry> patches, IProgress<(int percent, string status)> progress)
    {
        var dataDir = Path.Combine(_config.ClientPath, "Data");
        Directory.CreateDirectory(dataDir);

        long totalBytes = patches.Sum(p => p.SizeBytes);
        long doneBytes = 0;

        foreach (var patch in patches)
        {
            progress.Report((PercentOf(doneBytes, totalBytes), $"downloading {patch.FileName}"));

            var tempPath = Path.Combine(dataDir, patch.FileName + ".download");
            var finalPath = Path.Combine(dataDir, patch.FileName);

            // patch.Url is root-relative (e.g. "/Patch/Download?file=...") so it resolves correctly
            // whether we're talking to the real dashboard directly or through the scoped proxy.
            var downloadUrl = $"{_config.DashboardBaseUrl.TrimEnd('/')}{patch.Url}";
            using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using var httpStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);

                var buffer = new byte[81920];
                int read;
                while ((read = await httpStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    doneBytes += read;
                    progress.Report((PercentOf(doneBytes, totalBytes), $"downloading {patch.FileName}"));
                }
            }

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }

        progress.Report((100, "up to date"));
    }

    private static int PercentOf(long done, long total) =>
        total <= 0 ? 100 : (int)Math.Clamp(done * 100 / total, 0, 100);

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
