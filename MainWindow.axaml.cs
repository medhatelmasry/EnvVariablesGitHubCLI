using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace CopilotEnvSwitcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Load(BaseUrlBox, EnvStore.BaseUrl, "baseurls.txt");
        Load(ProviderBox, EnvStore.ProviderType, "providers.txt");
        // models.txt lines are "Engine,Model"; Text is the engine name matching a base URL entry.
        _allModels = EnvStore.ReadOptions("models.txt");
        RefreshModels(EnvStore.Get(EnvStore.Model));
        BaseUrlBox.SelectionChanged += (_, _) => RefreshModels(ValueOf(ModelBox));
        ApiKeyBox.Text = EnvStore.Get(EnvStore.ApiKey) ?? "";
        UseProvider.IsChecked = !string.IsNullOrEmpty(EnvStore.Get(EnvStore.ProviderType));
        UseApiKey.IsChecked = !string.IsNullOrEmpty(ApiKeyBox.Text);
        Optional_Changed(null, null!);
    }

    private List<EnvStore.Option> _allModels = new();

    // Shows only the models listed for the selected engine (base URL), plus a blank option.
    private void RefreshModels(string? preferred, bool keepUnlisted = true)
    {
        var engine = (BaseUrlBox.SelectedItem as EnvStore.Option)?.Text ?? "";
        var items = new List<EnvStore.Option> { new("", "") };
        items.AddRange(_allModels
            .Where(m => m.Value != "" && engine != "" && m.Text.Equals(engine, StringComparison.OrdinalIgnoreCase))
            .Select(m => new EnvStore.Option(m.Value, m.Value)));
        if (keepUnlisted && !string.IsNullOrEmpty(preferred) && !items.Any(o => o.Value == preferred))
            items.Add(new EnvStore.Option(preferred, preferred));
        ModelBox.ItemsSource = items;
        ModelBox.SelectedItem = items.FirstOrDefault(o => o.Value == (preferred ?? "")) ?? items[0];
    }

    private void Optional_Changed(object? sender, RoutedEventArgs e)
    {
        ProviderBox.IsEnabled = UseProvider.IsChecked == true;
        ApiKeyBox.IsEnabled = UseApiKey.IsChecked == true;
        ShowKey.IsEnabled = UseApiKey.IsChecked == true;
    }

    // Defaults to the current (last applied) value; falls back to the last item in the file.
    private static void Load(ComboBox box, string variable, string file)
    {
        var items = EnvStore.ReadOptions(file);
        var current = EnvStore.Get(variable);
        if (!string.IsNullOrEmpty(current) && !items.Any(o => o.Value == current))
            items.Add(new EnvStore.Option(current, current));
        if (!items.Any(o => o.Value == "")) items.Insert(0, new EnvStore.Option("", ""));
        box.ItemsSource = items;
        box.SelectedItem = !string.IsNullOrEmpty(current)
            ? items.First(o => o.Value == current)
            : items.First(o => o.Value == "");
    }

    private static string ValueOf(ComboBox box) => (box.SelectedItem as EnvStore.Option)?.Value ?? "";

    private void ShowKey_Changed(object? sender, RoutedEventArgs e) =>
        ApiKeyBox.PasswordChar = ShowKey.IsChecked == true ? '\0' : '●';

    private void ClearAll_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            EnvStore.Set(new Dictionary<string, string>
            {
                [EnvStore.BaseUrl] = "",
                [EnvStore.Model] = "",
                [EnvStore.ProviderType] = "",
                [EnvStore.ApiKey] = "",
            });
            EnvStore.MarkApplied();
            UseProvider.IsChecked = false;
            UseApiKey.IsChecked = false;
            ApiKeyBox.Text = "";
            StatusText.Text = "All Copilot environment variables cleared. Restart open terminals to pick up changes.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
    }

    private async void Apply_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            EnvStore.Set(new Dictionary<string, string>
            {
                [EnvStore.BaseUrl] =                 ValueOf(BaseUrlBox),
                                [EnvStore.Model] = ValueOf(ModelBox),
                                [EnvStore.ProviderType] = UseProvider.IsChecked == true ? ValueOf(ProviderBox) : "",
                                [EnvStore.ApiKey] = UseApiKey.IsChecked == true ? ApiKeyBox.Text ?? "" : "",
            });
            EnvStore.MarkApplied();
            StatusText.Text = OperatingSystem.IsWindows()
                ? "Saved to user environment variables. Restart open terminals to pick up changes."
                : "Saved to ~/.zshrc, ~/.bash_profile and launchctl. Open a new terminal to pick up changes.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
            return;
        }

        if (await AskExitAsync()) Close();
    }

    private async Task<bool> AskExitAsync()
    {
        var result = false;
        var dialog = new Window
        {
            Title = "Settings applied",
            Width = 400, Height = 220, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var yes = new Button { Content = "Exit", Padding = new Thickness(20, 6) };
        var no = new Button { Content = "Stay", Padding = new Thickness(20, 6) };
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                                new TextBlock { Text = "Settings applied. Do you want to exit the application?", TextWrapping = TextWrapping.Wrap },
                                                                new TextBlock
                                                                {
                                                                    Text = "Note: the changed environment variables will not affect terminal windows that are already open. Close and restart any open terminal window to pick up the changes.",
                                                                    TextWrapping = TextWrapping.Wrap,
                                                                    FontStyle = FontStyle.Italic,
                                                                },
                                                                new StackPanel
                                {
                                    Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right,
                                    Children = { yes, no },
                                },
            },
        };
        await dialog.ShowDialog(this);
        return result;
    }
}
