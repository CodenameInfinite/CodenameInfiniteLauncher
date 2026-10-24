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

    /// <summary>
    /// The WIP graphics client's exe name — resolves the WOW install's own Data folder from
    /// wherever this exe sits, so it just needs to be dropped into the existing ClientPath
    /// alongside Data/, no separate client folder.
    /// </summary>
    public string WipExeName { get; set; } = "benilla.exe";

    /// <summary>Opened via the OS default handler from the WIP Client toggle's prompt, same
    /// hand-off pattern as ClientDownloadUri — a GitHub releases page, typically.</summary>
    public string WipClientDownloadUri { get; set; } = "";

    /// <summary>Persisted so the player's choice survives a restart; PlayButton_Click reads
    /// this to pick WipExeName vs. ExeName within the same ClientPath.</summary>
    public bool UseWipClient { get; set; } = false;
}
