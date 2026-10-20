using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Deve1ne.Launcher.ViewModels;

namespace Deve1ne.Launcher.Views;

public partial class MainWindow : Window
{
    private const double MinCardWidth = 330;

    private readonly LauncherViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Confirm = (title, message, confirm) => new ConfirmWindow(title, message, confirm).ShowDialog<bool>(this);
        _viewModel.RequestExit = Close;

        this.FindControl<Button>("MinimizeButton")!.Click += (_, _) => WindowState = WindowState.Minimized;
        this.FindControl<Button>("MaximizeButton")!.Click += (_, _) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Close();

        // As many columns of cards as fit, like a responsive grid.
        var scroller = this.FindControl<ScrollViewer>("Scroller")!;
        scroller.SizeChanged += (_, e) =>
            _viewModel.Columns = Math.Max(1, (int)((e.NewSize.Width - 32) / MinCardWidth));

        // "Checked 3 min ago" stays true; a new check every hour while the window is open.
        var minutes = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += (_, _) =>
        {
            _viewModel.UpdateStatusText();
            if (++minutes % 120 == 0)
            {
                _ = _viewModel.CheckAsync();
            }
        };
        timer.Start();
        Opened += (_, _) => _ = _viewModel.CheckAsync();
        Closed += (_, _) => timer.Stop();
    }

    /// <summary>The "…" button of a card or row: actions of that app.</summary>
    private void OnMoreClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: AppCardViewModel app } button)
        {
            return;
        }

        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        void Add(string header, System.Windows.Input.ICommand command, bool enabled = true)
        {
            menu.Items.Add(new MenuItem { Header = header, Command = command, IsEnabled = enabled });
        }

        if (app.CanLaunch)
        {
            Add("Open", app.LaunchCommand);
        }

        if (app.IsMsi && app.InstalledVersion is not null)
        {
            Add("Show the install folder", app.OpenFolderCommand);
        }

        Add("Release notes", app.ReleaseNotesCommand, app.HasRelease);
        Add("GitHub", app.OpenGitHubCommand);
        if (app.IsMsi)
        {
            menu.Items.Add(new Separator());
            Add("Reinstall", app.ReinstallCommand, app.CanReinstall);
        }

        if (app.CanUninstall)
        {
            if (!app.IsMsi)
            {
                menu.Items.Add(new Separator());
            }

            Add("Uninstall…", app.UninstallCommand);
        }

        menu.ShowAt(button);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
