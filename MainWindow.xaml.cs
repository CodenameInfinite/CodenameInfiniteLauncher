using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CodenameInfiniteLauncher.Models;
using CodenameInfiniteLauncher.Services;

namespace CodenameInfiniteLauncher;

public partial class MainWindow : Window
{
    private readonly LauncherConfig _config;
    private readonly UpdateService _updateService;
    private readonly ClientVerifyService _clientVerifyService;
    private readonly SelfUpdateService _selfUpdateService = new();
    private readonly ClientWatchService _clientWatchService = new();
    private CancellationTokenSource? _downloadWatchCts;

    public MainWindow(LauncherConfig config)
    {
        InitializeComponent();
        _config = config;
        _updateService = new UpdateService(config);
        _clientVerifyService = new ClientVerifyService(config);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Self-update runs before anything else. On success it shuts this instance down and
        // relaunches the new one, so RunStartupFlowAsync never gets a chance to matter here.
        if (await CheckAndApplySelfUpdateAsync()) return;

        await RunStartupFlowAsync();
    }

    /// <summary>Returns true if an update was applied (the app is shutting down to relaunch).
    /// A failed check or a failed apply both fall through to false — self-update problems
    /// should never block someone from just playing on the version they already have.</summary>
    private async Task<bool> CheckAndApplySelfUpdateAsync()
    {
        StatusText.Text = "checking for launcher updates...";

        var update = await _selfUpdateService.CheckForUpdateAsync();
        if (update == null) return false;

        var progress = new Progress<(int percent, string status)>(p =>
        {
            UpdateProgress.Value = p.percent;
            StatusText.Text = p.status;
        });

        try
        {
            await _selfUpdateService.DownloadAndApplyUpdateAsync(update, progress);
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"launcher update failed ({ex.Message}), continuing with this version";
            UpdateProgress.Value = 0;
            return false;
        }
    }

    private async Task RunStartupFlowAsync()
    {
        if (!ClientIsInstalled())
        {
            FirstRunOverlay.Visibility = Visibility.Visible;
            SetReadyToLaunch(false);
            StatusText.Text = "waiting for client";
            return;
        }

        FirstRunOverlay.Visibility = Visibility.Collapsed;

        try
        {
            RealmlistService.EnsureRealmlist(_config.ClientPath, _config.RealmAddress);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"couldn't update realmlist.wtf: {ex.Message}";
        }

        await CheckForUpdatesAsync();
    }

    private bool ClientIsInstalled() =>
        !string.IsNullOrWhiteSpace(_config.ClientPath)
        && System.IO.File.Exists(System.IO.Path.Combine(_config.ClientPath, _config.ExeName));

    private void DownloadClientButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_config.ClientDownloadUri))
        {
            FirstRunDetailText.Text = "No client link configured yet — set clientDownloadUri in config.json, or use \"Locate existing install\" if you already have the client somewhere.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_config.ClientDownloadUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            FirstRunDetailText.Text = $"Couldn't open the download link: {ex.Message}";
            return;
        }

        FirstRunDetailText.Text = "If the download is an archive (.rar/.zip), extract it once it finishes — the launcher will pick it up automatically as soon as it sees the exe.";
        StartWatchingForClient();
    }

    /// <summary>
    /// Polls the downloads folder for the exe to show up so the user doesn't have to come back
    /// and click "Locate existing install" by hand once the torrent finishes. Cancelled if the
    /// user locates it manually first, or if a new download watch is started.
    /// </summary>
    private void StartWatchingForClient()
    {
        _downloadWatchCts?.Cancel();
        _downloadWatchCts = new CancellationTokenSource();
        var token = _downloadWatchCts.Token;

        var progress = new Progress<string>(status => FirstRunDetailText.Text = status);

        _ = WatchForClientAsync(token, progress);
    }

    private async Task WatchForClientAsync(CancellationToken token, IProgress<string> progress)
    {
        string? foundDir;
        try
        {
            foundDir = await _clientWatchService.WaitForClientAsync(_config, progress, token);
        }
        catch (Exception ex)
        {
            FirstRunDetailText.Text = $"Stopped watching for the client: {ex.Message}";
            return;
        }

        if (foundDir == null || token.IsCancellationRequested) return;

        _config.ClientPath = foundDir;
        ConfigService.Save(_config);
        FirstRunDetailText.Text = $"Found {_config.ExeName} — launching setup...";

        await RunStartupFlowAsync();
    }

    private async void LocateClientButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Locate WoW.exe",
            Filter = $"{_config.ExeName}|{_config.ExeName}|Executable (*.exe)|*.exe",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true) return;

        _downloadWatchCts?.Cancel();

        var chosenDir = System.IO.Path.GetDirectoryName(dialog.FileName);
        if (string.IsNullOrEmpty(chosenDir)) return;

        _config.ClientPath = chosenDir;
        ConfigService.Save(_config);

        await RunStartupFlowAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        StatusText.Text = "checking for updates...";
        UpdateProgress.Value = 0;
        SetReadyToLaunch(false);

        Manifest manifest;
        try
        {
            manifest = await _updateService.FetchManifestAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"couldn't reach the server: {ex.Message}";
            StatusPillText.Text = "realm unreachable";
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xE2, 0x4B, 0x4A));
            // Allow playing anyway — the server might just be unreachable for the launcher,
            // not the game client (e.g. dashboard down but mangosd/realmd still up).
            SetReadyToLaunch(true);
            return;
        }

        StatusPillText.Text = manifest.Realm.Online
            ? $"realm online — {manifest.Realm.OnlinePlayers} players"
            : "realm offline";
        StatusDot.Fill = new SolidColorBrush(manifest.Realm.Online
            ? Color.FromRgb(0x63, 0xC1, 0x5B)
            : Color.FromRgb(0x88, 0x88, 0x88));

        NewsList.ItemsSource = manifest.News
            .Select(n => $"{n.Date} — {n.Title}\n{n.Body}")
            .ToList();

        var outdated = _updateService.GetOutdatedPatches(manifest);

        if (outdated.Count == 0)
        {
            StatusText.Text = "up to date";
            UpdateProgress.Value = 100;
            SetReadyToLaunch(true);
            return;
        }

        var progress = new Progress<(int percent, string status)>(p =>
        {
            UpdateProgress.Value = p.percent;
            StatusText.Text = p.status;
        });

        try
        {
            await _updateService.DownloadPatchesAsync(outdated, progress);
            StatusText.Text = "up to date";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"update failed: {ex.Message}";
        }

        SetReadyToLaunch(true);
    }

    /// <summary>
    /// Flips the Play button and fades the background accent layer (shimmer/glow hotspots/
    /// twinkles) in or out together, so the art only "comes alive" once launch is actually ready.
    /// Also gates "Verify client files" — it shouldn't run while the quick patch check/download
    /// or another verify pass is already in flight.
    /// </summary>
    private void SetReadyToLaunch(bool ready)
    {
        PlayButton.IsEnabled = ready;
        VerifyFilesButton.IsEnabled = ready;
        BackgroundAccents.BeginAnimation(OpacityProperty, new DoubleAnimation(ready ? 1.0 : 0.0, TimeSpan.FromSeconds(ready ? 1.2 : 0.5)));
    }

    /// <summary>
    /// On-demand full base-client integrity check: hashes every base MPQ plus the exe against
    /// the dashboard's reference copy and repairs anything that doesn't match. Deliberately not
    /// run automatically on every launch — see ClientVerifyService's class doc for why.
    /// </summary>
    private async void VerifyFilesButton_Click(object sender, RoutedEventArgs e)
    {
        SetReadyToLaunch(false);
        StatusText.Text = "fetching base file manifest...";
        UpdateProgress.Value = 0;

        BaseManifest manifest;
        try
        {
            manifest = await _clientVerifyService.FetchBaseManifestAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"couldn't verify: {ex.Message}";
            SetReadyToLaunch(true);
            return;
        }

        var progress = new Progress<(int percent, string status)>(p =>
        {
            UpdateProgress.Value = p.percent;
            StatusText.Text = p.status;
        });

        List<BaseFileEntry> outdated;
        try
        {
            outdated = await _clientVerifyService.GetOutdatedBaseFilesAsync(manifest, progress);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"verification failed: {ex.Message}";
            SetReadyToLaunch(true);
            return;
        }

        if (outdated.Count == 0)
        {
            StatusText.Text = "all base files verified";
            UpdateProgress.Value = 100;
            SetReadyToLaunch(true);
            return;
        }

        try
        {
            await _clientVerifyService.DownloadBaseFilesAsync(outdated, progress);
            StatusText.Text = $"repaired {outdated.Count} base file(s)";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"repair failed: {ex.Message}";
        }

        SetReadyToLaunch(true);
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        var exePath = System.IO.Path.Combine(_config.ClientPath, _config.ExeName);
        if (!System.IO.File.Exists(exePath))
        {
            StatusText.Text = $"can't find {_config.ExeName} in {_config.ClientPath}";
            return;
        }

        Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = _config.ClientPath, UseShellExecute = true });
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
