using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Deve1ne.Launcher.Views;

/// <summary>A yes / no question in the launcher's style.</summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow() => InitializeComponent();

    public ConfirmWindow(string title, string message, string confirmLabel) : this()
    {
        this.FindControl<TextBlock>("TitleText")!.Text = title;
        this.FindControl<TextBlock>("MessageText")!.Text = message;
        this.FindControl<TextBlock>("ConfirmText")!.Text = confirmLabel;
        this.FindControl<Button>("ConfirmButton")!.Click += (_, _) => Close(true);
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close(false);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
