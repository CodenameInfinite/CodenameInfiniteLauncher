using System.IO;
using CodenameInfiniteLauncher.Models;

namespace CodenameInfiniteLauncher.Services;

/// <summary>
/// Polls a downloads folder for the game exe to appear after the user's torrent client
/// finishes, so the launcher can pick it up automatically instead of requiring the manual
/// "Locate existing install" file picker every time.
/// </summary>
public class ClientWatchService
{
    private const int PollIntervalMs = 3000;

    /// <summary>
    /// Resolves the folder to watch: the configured TorrentDownloadPath, or the user's
    /// Downloads folder if that's left blank.
    /// </summary>
    public static string ResolveWatchFolder(LauncherConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.TorrentDownloadPath))
            return config.TorrentDownloadPath;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    /// <summary>
    /// Polls until ExeName shows up under the watch folder (recursively) and its size has
    /// stopped changing between two consecutive checks (a cheap proxy for "the torrent client
    /// is done writing it"), or the token is cancelled. Returns the containing directory, or
    /// null if cancelled.
    /// </summary>
    public async Task<string?> WaitForClientAsync(LauncherConfig config, IProgress<string>? progress, CancellationToken token)
    {
        var watchFolder = ResolveWatchFolder(config);
        progress?.Report($"watching {watchFolder} for {config.ExeName}...");

        long? lastSeenSize = null;
        string? lastSeenPath = null;

        while (!token.IsCancellationRequested)
        {
            var found = TryFindExe(watchFolder, config.ExeName);
            if (found != null)
            {
                var size = new FileInfo(found).Length;

                if (found == lastSeenPath && size == lastSeenSize && size > 0 && !IsFileLocked(found))
                {
                    return Path.GetDirectoryName(found);
                }

                lastSeenPath = found;
                lastSeenSize = size;
                progress?.Report($"found {config.ExeName}, waiting for the download to finish...");
            }

            try
            {
                await Task.Delay(PollIntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    private static string? TryFindExe(string folder, string exeName)
    {
        if (!Directory.Exists(folder)) return null;

        try
        {
            return Directory.EnumerateFiles(folder, exeName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsFileLocked(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
