using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using CodenameInfiniteLauncher.Models;

namespace CodenameInfiniteLauncher.Services;

/// <summary>
/// On-demand full base-client integrity check — separate from UpdateService's always-on
/// startup patch check, which stays fast on purpose. This one hashes every base MPQ plus the
/// exe (several GB), so it's only run when the player explicitly asks for it.
/// </summary>
public class ClientVerifyService
{
    private readonly HttpClient _http;
    private readonly LauncherConfig _config;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const int BufferSize = 1024 * 1024; // 1MB — big files, no need for the small patch buffer size

    public ClientVerifyService(LauncherConfig config)
    {
        _config = config;
        // Cold-cache hashing on the server side can take tens of seconds for a multi-GB
        // client; give this its own generous timeout rather than reusing UpdateService's 30s.
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public async Task<BaseManifest> FetchBaseManifestAsync()
    {
        var url = $"{_config.DashboardBaseUrl.TrimEnd('/')}/Launcher/BaseManifest";
        var json = await _http.GetStringAsync(url);
        return JsonSerializer.Deserialize<BaseManifest>(json, JsonOpts)
            ?? throw new InvalidOperationException("Base manifest response was empty.");
    }

    /// <summary>Resolves where a base file actually lives locally: the exe sits in the client
    /// root, everything else (MPQs) sits in Data\.</summary>
    private string LocalPathFor(BaseFileEntry entry) =>
        string.Equals(entry.FileName, _config.ExeName, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(_config.ClientPath, entry.FileName)
            : Path.Combine(_config.ClientPath, "Data", entry.FileName);

    /// <summary>Hashes every local base file and compares against the manifest, reporting
    /// byte-level progress across the whole set (not just per-file) since a single MPQ can
    /// take real time to hash on its own.</summary>
    public async Task<List<BaseFileEntry>> GetOutdatedBaseFilesAsync(BaseManifest manifest, IProgress<(int percent, string status)> progress)
    {
        var outdated = new List<BaseFileEntry>();
        long totalBytes = manifest.Files.Sum(f => f.SizeBytes);
        long doneBytes = 0;

        foreach (var entry in manifest.Files)
        {
            var localPath = LocalPathFor(entry);
            progress.Report((PercentOf(doneBytes, totalBytes), $"verifying {entry.FileName}..."));

            if (!File.Exists(localPath))
            {
                outdated.Add(entry);
                doneBytes += entry.SizeBytes;
                continue;
            }

            var localHash = await ComputeSha256WithProgressAsync(localPath, doneBytes, totalBytes, entry.FileName, progress);
            doneBytes += entry.SizeBytes;

            if (!string.Equals(localHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                outdated.Add(entry);
        }

        progress.Report((100, outdated.Count == 0 ? "all base files verified" : $"{outdated.Count} file(s) need repair"));
        return outdated;
    }

    /// <summary>Downloads each outdated base file into its correct location (Data\ for MPQs,
    /// the client root for the exe), same atomic temp-then-move pattern as patch downloads.</summary>
    public async Task DownloadBaseFilesAsync(List<BaseFileEntry> files, IProgress<(int percent, string status)> progress)
    {
        long totalBytes = files.Sum(f => f.SizeBytes);
        long doneBytes = 0;

        foreach (var entry in files)
        {
            var finalPath = LocalPathFor(entry);
            var dir = Path.GetDirectoryName(finalPath)!;
            Directory.CreateDirectory(dir);
            var tempPath = finalPath + ".download";

            progress.Report((PercentOf(doneBytes, totalBytes), $"downloading {entry.FileName}"));

            var downloadUrl = $"{_config.DashboardBaseUrl.TrimEnd('/')}{entry.Url}";
            using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                using var httpStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

                var buffer = new byte[BufferSize];
                int read;
                while ((read = await httpStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    doneBytes += read;
                    progress.Report((PercentOf(doneBytes, totalBytes), $"downloading {entry.FileName}"));
                }
            }

            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tempPath, finalPath);
        }

        progress.Report((100, "base files repaired"));
    }

    private static async Task<string> ComputeSha256WithProgressAsync(
        string path, long baseDoneBytes, long totalBytes, string fileName, IProgress<(int percent, string status)> progress)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();

        var buffer = new byte[BufferSize];
        int read;
        long fileBytesRead = 0;

        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            sha.TransformBlock(buffer, 0, read, null, 0);
            fileBytesRead += read;
            progress.Report((PercentOf(baseDoneBytes + fileBytesRead, totalBytes), $"verifying {fileName}..."));
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static int PercentOf(long done, long total) =>
        total <= 0 ? 100 : (int)Math.Clamp(done * 100 / total, 0, 100);
}
