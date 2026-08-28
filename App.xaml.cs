using System.Windows;
using PoopCraftLauncher.Services;

namespace PoopCraftLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = ConfigService.Load();
        var window = new MainWindow(config);
        window.Show();
    }
}
