namespace PoopCraftLauncher.Models;

public class LauncherConfig
{
    public string DashboardBaseUrl { get; set; } = "http://localhost:5000";
    public string ClientPath { get; set; } = "";
    public string ExeName { get; set; } = "WoW.exe";
    public string RealmAddress { get; set; } = "127.0.0.1";
    public string ClientMagnetUri { get; set; } = "";
}
