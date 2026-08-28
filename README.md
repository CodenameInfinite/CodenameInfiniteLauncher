# CodenameInfinite Launcher

WPF launcher for the CodenameInfinite VMaNGOS 1.12.1 server. Points `realmlist.wtf` at the
configured server, fetches a manifest from the dashboard (via [CodenameInfiniteLauncherProxy](https://github.com/CodenameInfinite/CodenameInfiniteLauncherProxy)),
hash-diffs local patch MPQs against it, downloads what's changed, and launches `WoW.exe`.

If no client is found at the configured path, it offers a download link (if configured —
a magnet URI, a direct link, or a share link like Drive/Dropbox; the launcher just hands
whatever's there to the OS default handler) or lets you point it at an existing install.
After opening the link it also polls `torrentDownloadPath` (defaults to the user's
Downloads folder) for the exe to appear, so most people never need the manual file picker
— though an archive (.rar/.zip) still needs to be extracted by hand before it'll be found.

## Setup

1. `dotnet build`
2. First run copies `config.example.json` to `config.json` — edit `config.json` with your
   real client path, dashboard/proxy URL, realm address, and (optional) client download link.
   `config.json` is gitignored, so your local paths never get committed.

## Publish a standalone exe

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
