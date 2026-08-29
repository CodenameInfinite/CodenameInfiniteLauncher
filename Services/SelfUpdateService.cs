using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using CodenameInfiniteLauncher.Models;

namespace CodenameInfiniteLauncher.Services;

/// <summary>
/// Full auto-update: checks GitHub's latest release against the running assembly version,
/// and if newer, downloads it, hands off to a small PowerShell script that waits for this
/// process to exit, swaps the files, and relaunches — then shuts this instance down.
///
/// The swap script never touches config.json (the user's real settings), only the exe/dlls/
/// config.example.json — same "personal config is sacred" rule as everywhere else in this app.
/// </summary>
public class SelfUpdateService
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/CodenameInfinite/CodenameInfiniteLauncher/releases/latest";
    private readonly HttpClient _http;

    public SelfUpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // GitHub's API rejects unauthenticated requests with no User-Agent.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("CodenameInfiniteLauncher");
    }

    public record UpdateInfo(string Version, string DownloadUrl);

    /// <summary>Returns null if no update is available (including on any failure — a broken
    /// update check should never block normal startup).</summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var json = await _http.GetStringAsync(ReleasesApiUrl);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release == null || string.IsNullOrWhiteSpace(release.TagName)) return null;

            var latestStr = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(latestStr, out var latest)) return null;

            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0);
            if (!IsNewer(latest, current)) return null;

            var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            if (asset == null || string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)) return null;

            return new UpdateInfo(release.TagName, asset.BrowserDownloadUrl);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsNewer(Version latest, Version current)
    {
        if (latest.Major != current.Major) return latest.Major > current.Major;
        if (latest.Minor != current.Minor) return latest.Minor > current.Minor;
        return Math.Max(latest.Build, 0) > Math.Max(current.Build, 0);
    }

    /// <summary>Downloads and stages the update, then hands off to a swap script and shuts the
    /// app down. Only returns normally on failure (e.g. bad zip) — success ends in a shutdown.</summary>
    public async Task DownloadAndApplyUpdateAsync(UpdateInfo update, IProgress<(int percent, string status)> progress)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CNILauncherUpdate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, "update.zip");
        var extractDir = Path.Combine(tempDir, "extracted");

        progress.Report((0, $"downloading launcher {update.Version}..."));

        using (var response = await _http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? 0;
            long done = 0;

            using var httpStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write);

            var buffer = new byte[81920];
            int read;
            while ((read = await httpStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                var pct = total > 0 ? (int)Math.Clamp(done * 100 / total, 0, 100) : 0;
                progress.Report((pct, $"downloading launcher {update.Version}..."));
            }
        }

        progress.Report((100, "extracting update..."));
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var newExePath = Path.Combine(extractDir, "CodenameInfiniteLauncher.exe");
        if (!File.Exists(newExePath))
            throw new InvalidOperationException("Downloaded update is missing the launcher exe — aborting, nothing was replaced.");

        var currentExePath = Process.GetCurrentProcess().MainModule!.FileName!;
        var installDir = Path.GetDirectoryName(currentExePath)!;
        var currentPid = Process.GetCurrentProcess().Id;

        var scriptPath = Path.Combine(tempDir, "apply_update.ps1");
        File.WriteAllText(scriptPath, BuildSwapScript(currentPid, extractDir, installDir, currentExePath, tempDir));

        progress.Report((100, "restarting to apply update..."));

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = true,
            CreateNoWindow = true,
        });

        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }

    private static string BuildSwapScript(int pid, string extractDir, string installDir, string exePath, string tempDir) => $$"""
        $ErrorActionPreference = 'Stop'
        try { Wait-Process -Id {{pid}} -Timeout 30 -ErrorAction SilentlyContinue } catch {}
        Start-Sleep -Milliseconds 500

        Get-ChildItem -Path '{{extractDir}}' -Recurse -File | ForEach-Object {
            if ($_.Name -ieq 'config.json') { return }
            $relative = $_.FullName.Substring('{{extractDir}}'.Length + 1)
            $dest = Join-Path '{{installDir}}' $relative
            $destDir = Split-Path $dest -Parent
            if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
            Copy-Item -Path $_.FullName -Destination $dest -Force
        }

        Start-Process -FilePath '{{exePath}}' -WorkingDirectory '{{installDir}}'
        Start-Sleep -Milliseconds 500
        Remove-Item -Path '{{tempDir}}' -Recurse -Force -ErrorAction SilentlyContinue
        """;
}
