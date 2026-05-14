using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ShadowPilot.Services;

namespace ShadowPilot;

public partial class OverlayWindow : Window
{
    public AppViewModel ViewModel { get; } = new();

    // Win32: make window non-activating (clicks don't steal focus)
    private const int  GWL_EXSTYLE        = -20;
    private const int  WS_EX_NOACTIVATE   = 0x08000000;
    private const int  WS_EX_TOOLWINDOW   = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    public OverlayWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;

        ViewModel.PropertyChanged += OnVmChanged;

        Loaded += (_, _) =>
        {
            // Set non-activating extended style
            var hwnd  = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

            // Register global hotkeys
            HotkeyManager.Shared.Register(this);
            HotkeyManager.Shared.OnMicToggle  = () => ViewModel.IsListening ? ViewModel.StopListening() : ViewModel.StartListening();
            HotkeyManager.Shared.OnGetAnswer  = () => _ = ViewModel.GetAnswerAsync();
            HotkeyManager.Shared.OnScreenshot = () => _ = ViewModel.CaptureAndAnalyzeAsync();
            HotkeyManager.Shared.OnClear      = () => ViewModel.Clear();

            // Center horizontally at top of primary screen
            var screen = SystemParameters.WorkArea;
            Left = (screen.Width - Width) / 2;
            Top  = 30;
        };

        Closing += (_, _) => HotkeyManager.Shared.Unregister(this);
    }

    // ── Drag to move ──────────────────────────────────────────────────────────
    private void RootBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    // ── Button handlers ───────────────────────────────────────────────────────
    private void MicBtn_Click(object s, RoutedEventArgs e)
    {
        if (ViewModel.IsListening) ViewModel.StopListening();
        else ViewModel.StartListening();
    }

    private void AnswerBtn_Click(object s, RoutedEventArgs e) => _ = ViewModel.GetAnswerAsync();

    private void ScreenBtn_Click(object s, RoutedEventArgs e) => _ = ViewModel.CaptureAndAnalyzeAsync();

    private void WriteBtn_Click(object s, RoutedEventArgs e)
    {
        ViewModel.IsWriting = !ViewModel.IsWriting;
        if (ViewModel.IsWriting)
        {
            WriteField.Text = ViewModel.Transcript;
            WriteField.Visibility = Visibility.Visible;
            StatusTb.Visibility   = Visibility.Collapsed;
            WriteField.Focus();
            WriteField.CaretIndex = WriteField.Text.Length;
        }
        else
        {
            WriteField.Visibility = Visibility.Collapsed;
            StatusTb.Visibility   = Visibility.Visible;
        }
    }

    private void WriteField_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            ViewModel.Transcript = WriteField.Text;
            ViewModel.IsWriting  = false;
            WriteField.Visibility = Visibility.Collapsed;
            StatusTb.Visibility   = Visibility.Visible;
            _ = ViewModel.GetAnswerAsync();
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.IsWriting   = false;
            WriteField.Visibility = Visibility.Collapsed;
            StatusTb.Visibility   = Visibility.Visible;
        }
    }

    private void ClearBtn_Click(object s, RoutedEventArgs e)
    {
        ViewModel.Clear();
        WriteField.Visibility = Visibility.Collapsed;
        StatusTb.Visibility   = Visibility.Visible;
    }

    private void ClearHistoryBtn_Click(object s, RoutedEventArgs e) => ViewModel.ClearHistory();

    // ── Pill toggles ──────────────────────────────────────────────────────────
    private void FollowUpPill_Changed(object s, RoutedEventArgs e)
    {
        ViewModel.FollowUpMode = FollowUpPill.IsChecked == true;
        UpdatePillColors();
    }
    private void WhisperPill_Changed(object s, RoutedEventArgs e)
    {
        ViewModel.WhisperMode = WhisperPill.IsChecked == true;
        UpdatePillColors();
    }
    private void AutoPill_Changed(object s, RoutedEventArgs e)
    {
        ViewModel.AutoListen = AutoPill.IsChecked == true;
        UpdatePillColors();
    }

    private static readonly SolidColorBrush Green  = new(Color.FromRgb(0x59, 0xE6, 0x8C));
    private static readonly SolidColorBrush Blue   = new(Color.FromRgb(0x73, 0xC7, 0xFF));
    private static readonly SolidColorBrush Amber  = new(Color.FromRgb(0xFC, 0xC2, 0x47));
    private static readonly SolidColorBrush Subtext = new(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF));

    private void UpdatePillColors()
    {
        SetPillColor(FollowUpIcon, FollowUpLabel, FollowUpPill.IsChecked == true, Green);
        SetPillColor(WhisperIcon,  WhisperLabel,  WhisperPill.IsChecked  == true, Blue);
        SetPillColor(AutoIcon,     AutoLabel,     AutoPill.IsChecked     == true, Amber);

        // Show/hide clear history button
        if (ViewModel.FollowUpMode && ViewModel.History.Count > 0)
        {
            ClearHistoryBtn.Visibility = Visibility.Visible;
            ClearHistoryTb.Text = $"Clear history ({ViewModel.History.Count})";
        }
        else
        {
            ClearHistoryBtn.Visibility = Visibility.Collapsed;
        }
    }

    private static void SetPillColor(System.Windows.Controls.TextBlock icon,
                                     System.Windows.Controls.TextBlock label,
                                     bool on, SolidColorBrush color)
    {
        icon.Foreground  = on ? color : Subtext;
        label.Foreground = on ? color : Subtext;
    }

    // ── ViewModel reactive updates ────────────────────────────────────────────
    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(AppViewModel.StatusText):
                    StatusTb.Text = ViewModel.StatusText;
                    StatusTb.Foreground = ViewModel.IsListening ? (Brush)FindResource("SpText") : (Brush)FindResource("SpSubtext");
                    break;

                case nameof(AppViewModel.IsListening):
                    MicIcon.Text = ViewModel.IsListening ? "⏹" : "🎙";
                    MicBtn.Background = ViewModel.IsListening
                        ? new SolidColorBrush(Color.FromArgb(0x25, 0xFB, 0x52, 0x52))
                        : (Brush)FindResource("SpSurface");
                    Waveform.IsActive = ViewModel.IsListening;
                    break;

                case nameof(AppViewModel.IsCapturing):
                    ScreenIcon.Text = ViewModel.IsCapturing ? "⌛" : "📷";
                    break;

                case nameof(AppViewModel.ShowFiller):
                    FillerBanner.Visibility = ViewModel.ShowFiller ? Visibility.Visible : Visibility.Collapsed;
                    break;

                case nameof(AppViewModel.FillerText):
                    FillerTb.Text = ViewModel.FillerText;
                    break;

                case nameof(AppViewModel.ShowAnswer):
                    AnswerPanel.Visibility = ViewModel.ShowAnswer ? Visibility.Visible : Visibility.Collapsed;
                    break;

                case nameof(AppViewModel.IsLoadingAnswer):
                    LoadingDots.Visibility = ViewModel.IsLoadingAnswer ? Visibility.Visible : Visibility.Collapsed;
                    AnswerScroll.Visibility = ViewModel.IsLoadingAnswer ? Visibility.Collapsed : Visibility.Visible;
                    break;

                case nameof(AppViewModel.Answer):
                    AnswerMd.Markdown = ViewModel.Answer;
                    break;

                case nameof(AppViewModel.Transcript):
                    TranscriptTb.Text = ViewModel.Transcript;
                    TranscriptStrip.Visibility = string.IsNullOrEmpty(ViewModel.Transcript)
                        ? Visibility.Collapsed : Visibility.Visible;
                    TranscriptDivider.Visibility = TranscriptStrip.Visibility;
                    break;
            }
        });
    }
}
