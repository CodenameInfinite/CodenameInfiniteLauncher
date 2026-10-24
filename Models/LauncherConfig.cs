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

    /// <summary>The WIP graphics client's own dedicated install folder — a whole separate
    /// install from ClientPath, not a file dropped alongside the stock exe. Empty until
    /// WipClientService.DownloadAndInstallAsync creates it (defaults to a sibling of
    /// ClientPath the first time, see MainWindow's WipClientPath-or-default resolution).
    /// Holds nothing but the exe: no Data/ copy, no duplicated GBs — PlayButton_Click points
    /// its process at ClientPath's existing Data/ via the WOW_DATA environment variable
    /// instead, same install either way.</summary>
    public string WipClientPath { get; set; } = "";

    /// <summary>The WIP client's exe name, resolved within WipClientPath (not ClientPath).</summary>
    public string WipExeName { get; set; } = "benilla.exe";

    /// <summary>The release tag of whatever's currently sitting in WipClientPath — set after a
    /// successful DownloadAndInstallAsync. Compared against the latest release's tag every time
    /// WIP Client is turned on, so toggling it off and back on (with the exe already present)
    /// doesn't skip a real update — exe-presence alone can't tell an old install from a current
    /// one.</summary>
    public string WipInstalledVersion { get; set; } = "";

    /// <summary>Opened via the OS default handler from the WIP Client toggle's prompt, same
    /// hand-off pattern as ClientDownloadUri — a GitHub releases page, typically.</summary>
    public string WipClientDownloadUri { get; set; } = "";

    /// <summary>Persisted so the player's choice survives a restart; PlayButton_Click reads
    /// this to pick the WIP client (WipClientPath/WipExeName + ClientPath's Data via WOW_DATA)
    /// vs. the stock one (ClientPath/ExeName, unchanged).</summary>
    public bool UseWipClient { get; set; } = false;
}
