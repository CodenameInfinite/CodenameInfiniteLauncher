# PoopCraft Launcher

WPF launcher for the PoopCraft VMaNGOS 1.12.1 server. Points `realmlist.wtf` at the
configured server, fetches a manifest from the dashboard (via [PoopCraftLauncherProxy](https://github.com/CodenameInfinite/PoopCraftLauncherProxy)),
hash-diffs local patch MPQs against it, downloads what's changed, and launches `WoW.exe`.

If no client is found at the configured path, it offers a torrent link (if configured)
or lets you point it at an existing install.

## Setup

1. `dotnet build`
2. First run copies `config.example.json` to `config.json` — edit `config.json` with your
   real client path, dashboard/proxy URL, realm address, and (optional) client magnet URI.
   `config.json` is gitignored, so your local paths never get committed.

## Publish a standalone exe

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
