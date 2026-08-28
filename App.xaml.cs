using System.Windows;
using CodenameInfiniteLauncher.Services;

namespace CodenameInfiniteLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DesktopShortcutService.EnsureShortcutExists();

        var config = ConfigService.Load();
        var window = new MainWindow(config);
        window.Show();
    }
}
