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

/// <summary>Response shape of GET /Launcher/BaseManifest.</summary>
public class BaseManifest
{
    public List<BaseFileEntry> Files { get; set; } = new();
}

/// <summary>A base client file (MPQ or the exe) — same shape as PatchEntry, kept as its own
/// type since it's a separate on-demand endpoint, not part of the always-fetched Manifest.</summary>
public class BaseFileEntry
{
    public string FileName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string Url { get; set; } = "";
}

/// <summary>Response shape of GET /Downloads/AddonList. "Folder" is also the Interface\AddOns\
/// subfolder name to install into and the SavedVariables-scoping identity, so it's the key
/// used everywhere (install state, matching against a local copy) rather than Title.</summary>
public class AddonsResponse
{
    public List<AddonEntry> Addons { get; set; } = new();
}

public class AddonEntry
{
    public string Folder { get; set; } = "";
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string Url { get; set; } = "";
}
