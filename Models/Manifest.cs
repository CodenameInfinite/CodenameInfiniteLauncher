namespace CodenameInfiniteLauncher.Models;

public class Manifest
{
    public RealmInfo Realm { get; set; } = new();
    public List<PatchEntry> Patches { get; set; } = new();
    public List<NewsEntry> News { get; set; } = new();
}

public class RealmInfo
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Online { get; set; }
    public int OnlinePlayers { get; set; }
}

public class PatchEntry
{
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string Url { get; set; } = "";
}

public class NewsEntry
{
    public string Date { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
}
