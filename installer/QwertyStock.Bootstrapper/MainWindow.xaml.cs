using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace QwertyStock.Bootstrapper;

public partial class MainWindow : Window
{
    private readonly InstallerLogger _log = new();
    private readonly InstallerOrchestrator _orchestrator;

    public MainWindow()
    {
        SourceInitialized += (_, _) => DwmWindowInterop.TryApplyRoundedCorners(this);
        InitializeComponent();
        _orchestrator = new InstallerOrchestrator(_log);
        BrandLine1.Text = InstallerStrings.BrandLine1;
        BrandLine2.Text = InstallerStrings.BrandLine2;
        TaglineText.Text = InstallerStrings.Tagline;
        IntroText.Text = InstallerStrings.IntroBody;
        StatusText.Text = InstallerStrings.StatusStarting;
        VersionText.Text = InstallerStrings.FormatVersion(AppVersion.Semantic);
        CloseButton.ToolTip = InstallerStrings.CloseTooltip;
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        if (IsDescendantOfCloseButton(e.OriginalSource as DependencyObject))
            return;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // ignore: drag not started from client area in edge cases
        }
    }

    private bool IsDescendantOfCloseButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, CloseButton))
                return true;
            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        var progress = new Progress<InstallProgress>(p =>
        {
            ProgressBar.IsIndeterminate = p.Indeterminate;
            if (!p.Indeterminate)
                ProgressBar.Value = p.Percent;
            StatusText.Text = p.Message;
        });
        try
        {
            await _orchestrator.RunAsync(progress, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log.Error("Installation failed", ex);
            ProgressBar.IsIndeterminate = false;
            StatusText.Text = ex.Message;
            MessageBox.Show(ex.Message, InstallerStrings.AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
