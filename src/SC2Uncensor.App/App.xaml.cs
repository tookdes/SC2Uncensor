using System.Windows;
using SC2Uncensor.Core;
using SC2Uncensor.Core.Input;

namespace SC2Uncensor.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (!UncensorService.TakeSingleInstance())
        {
            MessageBox.Show("SC2Uncensor is already running.", "SC2 Uncensor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(2);
            return;
        }
        Log.Open(AppPaths.LogFile);
        Log.Banner($"SC2 Uncensor {UncensorService.Version}");
        var svc = new UncensorService();
        svc.TryAttach();
        var loop = new HotkeyLoop(svc.Config, svc);
        loop.Start();
        var win = new MainWindow(svc, loop);
        win.Show();
    }
}
