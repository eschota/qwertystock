using System.Windows;
using System.Windows.Media.Imaging;

namespace QwertyStock.Bootstrapper;

public partial class MainWindow : Window
{
    private readonly InstallerLogger _log = new();
    private readonly InstallerOrchestrator _orchestrator;

    public MainWindow()
    {
        InitializeComponent();
        _orchestrator = new InstallerOrchestrator(_log);
        try
        {
            LogoImage.Source = new BitmapImage(
                new Uri("pack://application:,,,/Assets/QS_LOGO.gif", UriKind.Absolute));
        }
        catch (Exception ex)
        {
            _log.Error("Could not load embedded logo", ex);
        }
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        var progress = new Progress<(int percent, string message)>(p =>
        {
            ProgressBar.Value = p.percent;
            StatusText.Text = p.message;
        });
        try
        {
            await _orchestrator.RunAsync(progress, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Error("Installation failed", ex);
            StatusText.Text = ex.Message;
            MessageBox.Show(ex.Message, "QwertyStock", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
