using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace QwertyStock.Bootstrapper;

public partial class MainWindow : Window
{
    private static readonly Regex ShortEtaLike = new(
        @"^\d+:\d{2}(:\d{2})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly InstallerLogger _log = new();
    private readonly InstallerOrchestrator _orchestrator;
    private TaskCompletionSource<bool>? _networkRetryTcs;
    private TaskCompletionSource? _fatalAckTcs;

    public MainWindow()
    {
        InitializeComponent();
        _orchestrator = new InstallerOrchestrator(_log);
        BrandLine1.Text = InstallerStrings.BrandLine1;
        BrandLine2.Text = InstallerStrings.BrandLine2;
        TaglineText.Text = InstallerStrings.Tagline;
        IntroText.Text = InstallerStrings.IntroBody;
        StatusText.Text = InstallerStrings.StatusStarting;
        DetailText.Text = "";
        VersionText.Text = InstallerStrings.FormatVersion(AppVersion.Semantic);
        CloseButton.ToolTip = InstallerStrings.CloseTooltip;
        OpenLogsButton.Content = InstallerStrings.OpenLogsFolderButton;
        RetryButton.Content = InstallerStrings.RetryInstall;
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

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        LogFolderOpener.OpenLogsFolder();
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorOverlay.Visibility = Visibility.Collapsed;
        _networkRetryTcs?.TrySetResult(true);
    }

    private void DismissErrorButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorOverlay.Visibility = Visibility.Collapsed;
        if (_networkRetryTcs != null)
            _networkRetryTcs.TrySetResult(false);
        else
            _fatalAckTcs?.TrySetResult();
    }

    /// <summary>Speed · ETA · bytes; skips unknown (—) parts.</summary>
    private static string FormatStatsLine(InstallProgress p)
    {
        static string? TakeMeaningful(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            var t = s.Trim();
            return t == "—" ? null : t;
        }

        var parts = new List<string>(3);
        var sp = TakeMeaningful(p.SpeedText);
        if (sp != null)
            parts.Add(sp);

        var et = TakeMeaningful(p.EtaText);
        if (et != null)
        {
            if (ShortEtaLike.IsMatch(et))
                parts.Add(InstallerStrings.ProgressShortEta(et));
            else
                parts.Add(et);
        }

        var bt = TakeMeaningful(p.BytesText);
        if (bt != null)
            parts.Add(bt);

        return parts.Count == 0 ? "" : string.Join("  ·  ", parts);
    }

    private void ApplyProgress(InstallProgress p)
    {
        ProgressBar.IsIndeterminate = p.Indeterminate;
        if (!p.Indeterminate)
            ProgressBar.Value = Math.Clamp(p.Percent, 0, 100);

        if (p.Indeterminate)
            PercentText.Text = "…";
        else
        {
            var x = Math.Clamp(p.Percent, 0, 100);
            PercentText.Text = x >= 100
                ? "100%"
                : string.Format(CultureInfo.InvariantCulture, "{0:0.#}%", x);
        }

        StatsText.Text = FormatStatsLine(p);
        StatsText.Visibility = string.IsNullOrEmpty(StatsText.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;

        StatusText.Text = p.Message;
        if (string.IsNullOrEmpty(p.Detail))
        {
            DetailText.Text = "";
            DetailText.Visibility = Visibility.Collapsed;
        }
        else
        {
            DetailText.Text = p.Detail;
            DetailText.Visibility = Visibility.Visible;
        }
    }

    private void ResetProgressUi()
    {
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = 0;
        PercentText.Text = "0%";
        StatsText.Text = "";
        StatsText.Visibility = Visibility.Collapsed;
        StatusText.Text = InstallerStrings.StatusStarting;
        DetailText.Text = "";
        DetailText.Visibility = Visibility.Collapsed;
    }

    private Task<bool> PromptNetworkRetryAsync(string message)
    {
        _networkRetryTcs = new TaskCompletionSource<bool>();
        ErrorTitleText.Text = InstallerStrings.NetworkErrorTitle;
        ErrorMessageText.Text = message;
        ErrorLogHintText.Text = InstallerStrings.LogFileHint;
        RetryButton.Visibility = Visibility.Visible;
        OpenLogsButton.Content = InstallerStrings.OpenLogsFolderButton;
        DismissErrorButton.Content = InstallerStrings.CancelRetryInstall;
        ErrorOverlay.Visibility = Visibility.Visible;
        return _networkRetryTcs.Task;
    }

    private Task WaitForFatalAckAsync(string message)
    {
        _fatalAckTcs = new TaskCompletionSource();
        ErrorTitleText.Text = InstallerStrings.ErrorInstallFailedTitle;
        ErrorMessageText.Text = message;
        ErrorLogHintText.Text = InstallerStrings.LogFileHint;
        RetryButton.Visibility = Visibility.Collapsed;
        OpenLogsButton.Content = InstallerStrings.OpenLogsFolderButton;
        DismissErrorButton.Content = InstallerStrings.CloseAfterError;
        ErrorOverlay.Visibility = Visibility.Visible;
        return _fatalAckTcs.Task;
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        var progress = new Progress<InstallProgress>(ApplyProgress);
        try
        {
            while (true)
            {
                try
                {
                    await _orchestrator.RunAsync(progress, CancellationToken.None);
                    Close();
                    return;
                }
                catch (Exception ex) when (NetworkErrors.IsLikelyNetworkRelated(ex))
                {
                    _log.Error("Installation failed (network)", ex);
                    var retry = await PromptNetworkRetryAsync(ex.Message);
                    _networkRetryTcs = null;
                    if (!retry)
                    {
                        ErrorOverlay.Visibility = Visibility.Collapsed;
                        Close();
                        return;
                    }

                    ErrorOverlay.Visibility = Visibility.Collapsed;
                    ResetProgressUi();
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("Installation failed", ex);
            ProgressBar.IsIndeterminate = false;
            StatsText.Text = "";
            StatsText.Visibility = Visibility.Collapsed;
            DetailText.Text = "";
            DetailText.Visibility = Visibility.Collapsed;
            StatusText.Text = ex.Message;
            await WaitForFatalAckAsync(ex.Message);
            _fatalAckTcs = null;
            Close();
        }
    }
}
