using System.Windows;
using System.Windows.Media;
using ShadowPilot.Services;

namespace ShadowPilot;

public partial class SetupWindow : Window
{
    private readonly Action _onDone;

    public SetupWindow(AppViewModel vm, Action onDone)
    {
        InitializeComponent();
        DataContext = vm;
        _onDone = onDone;

        var hasKeys = !string.IsNullOrEmpty(EnvConfig.BedrockKey)    ||
                      !string.IsNullOrEmpty(EnvConfig.OpenRouterKey) ||
                      !string.IsNullOrEmpty(EnvConfig.OpenAIKey);
        ApplyKeyStatus(hasKeys);
    }

    private void ApplyKeyStatus(bool hasKeys)
    {
        if (hasKeys)
        {
            KeyDot.Fill           = new SolidColorBrush(Colors.LimeGreen);
            KeyStatusText.Text    = "API keys loaded from .env";
            KeyStatusText.Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
            KeyStatusBorder.Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255));
            KeyStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
            KeyStatusBorder.BorderThickness = new Thickness(1);
            BeginBtn.IsEnabled = true;
            NoKeyHint.Visibility = Visibility.Collapsed;
        }
        else
        {
            KeyDot.Fill           = new SolidColorBrush(Color.FromRgb(0xFB, 0x52, 0x52));
            KeyStatusText.Text    = "No API keys — check %USERPROFILE%\\.shadowpilot.env";
            KeyStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xFB, 0x52, 0x52));
            KeyStatusBorder.Background  = new SolidColorBrush(Color.FromArgb(20, 0xFB, 0x52, 0x52));
            KeyStatusBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(75, 0xFB, 0x52, 0x52));
            KeyStatusBorder.BorderThickness = new Thickness(1);
            BeginBtn.IsEnabled = false;
            NoKeyHint.Visibility = Visibility.Visible;
        }
    }

    private void BeginBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppViewModel vm) vm.SaveSettings();
        _onDone();
    }
}
