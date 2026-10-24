namespace CodenameInfiniteLauncher.Models;

/// <summary>One row in the Addons overlay's list. Plain data, no INotifyPropertyChanged —
/// MainWindow re-binds AddonsList.ItemsSource to a fresh list after every action, matching
/// how NewsList is already refreshed wholesale rather than mutated in place.</summary>
public class AddonRow
{
    public AddonEntry Addon { get; set; } = null!;
    public string Title => Addon.Title;
    public string Notes => Addon.Notes;
    public string VersionText => string.IsNullOrEmpty(Addon.Version) ? "" : $"v{Addon.Version}";
    public string ButtonText { get; set; } = "";
    public bool ButtonEnabled { get; set; } = true;
}
