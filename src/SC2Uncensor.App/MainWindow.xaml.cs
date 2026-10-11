using System.Windows;
using SC2Uncensor.Core;
using SC2Uncensor.Core.Input;

namespace SC2Uncensor.App;

public partial class MainWindow : Window
{
    private readonly UncensorService _svc;
    private readonly HotkeyLoop _hotkeys;
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public MainWindow(UncensorService svc, HotkeyLoop hotkeys)
    {
        InitializeComponent();
        _svc = svc;
        _hotkeys = hotkeys;
        Log.LineWritten += line => Dispatcher.BeginInvoke(() =>
        {
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        });
        _hotkeys.ActionDone += (_, _) => Dispatcher.BeginInvoke(RefreshStatus);
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var st = _svc.PeekState();
        if (st is null)
            StatusText.Text = _svc.NoGameMessage();
        else
            StatusText.Text = $"NOP={st.Nop}  store={st.Store}  R={st.R}  ver={_svc.Memory?.ExeVersion}";
    }

    private void Run(string name) =>
        Task.Run(() => _hotkeys.Request(name));

    private void OnUncensor(object sender, RoutedEventArgs e) => Run("uncensor");
    private void OnUncensorAuto(object sender, RoutedEventArgs e) => Run("uncensorAuto");
    private void OnToggle(object sender, RoutedEventArgs e) => Run("clickToggle");
    private void OnStatus(object sender, RoutedEventArgs e) => Run("status");
    private void OnRestore(object sender, RoutedEventArgs e) => Run("restore");
    private void OnRestoreAuto(object sender, RoutedEventArgs e) => Run("restoreAuto");

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _hotkeys.Stop();
        _svc.Dispose();
        base.OnClosed(e);
    }
}
