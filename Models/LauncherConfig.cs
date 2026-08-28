namespace CodenameInfiniteLauncher.Models;

public class LauncherConfig
{
    public string DashboardBaseUrl { get; set; } = "http://localhost:5000";
    public string ClientPath { get; set; } = "";
    public string ExeName { get; set; } = "WoW.exe";
    public string RealmAddress { get; set; } = "127.0.0.1";
    /// <summary>
    /// Opened via the OS default handler when "Download client" is clicked — a magnet URI,
    /// a direct download link, or a share link (Drive, Dropbox, etc.). Whatever it is, the
    /// launcher just hands it to the shell; it doesn't fetch or verify the content itself.
    /// </summary>
    public string ClientDownloadUri { get; set; } = "";

    /// <summary>
    /// Folder to watch for ExeName appearing after the user's torrent client finishes.
    /// Empty means "fall back to the user's Downloads folder" (resolved at runtime).
    /// </summary>
    public string TorrentDownloadPath { get; set; } = "";
}
