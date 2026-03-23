using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using XamlAnimatedGif;

namespace QwertyStock.Bootstrapper;

public partial class MainWindow : Window
{
    private readonly InstallerLogger _log = new();
    private readonly InstallerOrchestrator _orchestrator;

    public MainWindow()
    {
        InitializeComponent();
        _orchestrator = new InstallerOrchestrator(_log);
        var gifUri = new Uri("pack://application:,,,/Assets/QS_LOGO.gif", UriKind.Absolute);
        AnimationBehavior.SetSourceUri(LogoImage, gifUri);
        AnimationBehavior.SetRepeatBehavior(LogoImage, RepeatBehavior.Forever);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
