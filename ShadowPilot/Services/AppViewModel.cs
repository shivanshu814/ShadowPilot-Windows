using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ShadowPilot.Models;

namespace ShadowPilot.Services;

public sealed class AppViewModel : INotifyPropertyChanged
{
    private static readonly string[] FillerPhrases =
    [
        "That's a great question — let me think through this...",
        "So, the way I approach this is...",
        "Yeah, I've dealt with this before — give me a second to structure my thoughts...",
        "Interesting — there are a few angles here...",
        "Right, so the core of this is...",
        "Let me walk you through how I'd think about this...",
        "Good question — I want to make sure I give you a complete answer...",
        "So off the top of my head...",
    ];

    // ── Published state ──────────────────────────────────────────────────────
    private bool   _isListening;
    private bool   _showAnswer;
    private bool   _isLoadingAnswer;
    private bool   _isCapturing;
    private bool   _isWriting  = true;
    private bool   _showFiller;
    private bool   _followUpMode;
    private bool   _whisperMode;
    private bool   _autoListen;
    private string _transcript   = "";
    private string _answer       = "";
    private string _statusText   = "Ready";
    private string _fillerText   = "";

    public bool   IsListening    { get => _isListening;    set => Set(ref _isListening, value); }
    public bool   ShowAnswer     { get => _showAnswer;     set => Set(ref _showAnswer, value); }
    public bool   IsLoadingAnswer{ get => _isLoadingAnswer;set => Set(ref _isLoadingAnswer, value); }
    public bool   IsCapturing    { get => _isCapturing;    set => Set(ref _isCapturing, value); }
    public bool   IsWriting      { get => _isWriting;      set => Set(ref _isWriting, value); }
    public bool   ShowFiller     { get => _showFiller;     set => Set(ref _showFiller, value); }
    public bool   FollowUpMode   { get => _followUpMode;   set => Set(ref _followUpMode, value); }
    public bool   WhisperMode    { get => _whisperMode;    set => Set(ref _whisperMode, value); }
    public bool   AutoListen     { get => _autoListen;     set => Set(ref _autoListen, value); }
    public string Transcript     { get => _transcript;     set => Set(ref _transcript, value); }
    public string Answer         { get => _answer;         set => Set(ref _answer, value); }
    public string StatusText     { get => _statusText;     set => Set(ref _statusText, value); }
    public string FillerText     { get => _fillerText;     set => Set(ref _fillerText, value); }

    public ObservableCollection<ConversationTurn> History { get; } = [];

    // Persisted settings (stored in user profile JSON)
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".shadowpilot_settings.json");

    public string JD     { get; set; } = "";
    public string Resume { get; set; } = "";

    // ── Services ─────────────────────────────────────────────────────────────
    private readonly SystemAudioCapture      _audioCapture    = new();
    private readonly SpeechRecognizerService _speechRec       = new();
    private readonly SilenceDetector         _silenceDetector = new(1.8);

    // Priority: Bedrock → OpenRouter → OpenAI
    private BedrockService? BedrockSvc    => string.IsNullOrEmpty(EnvConfig.BedrockKey)    ? null
        : new BedrockService(EnvConfig.BedrockKey, EnvConfig.BedrockRegion);
    private GPTService?     OpenRouterSvc => string.IsNullOrEmpty(EnvConfig.OpenRouterKey) ? null
        : new GPTService(EnvConfig.OpenRouterKey, "https://openrouter.ai/api/v1", "openai/gpt-4o");
    private GPTService?     OpenAISvc     => string.IsNullOrEmpty(EnvConfig.OpenAIKey)     ? null
        : new GPTService(EnvConfig.OpenAIKey);

    private CancellationTokenSource? _whisperCts;

    public AppViewModel() => LoadSettings();

    // ── Listening ─────────────────────────────────────────────────────────────
    public void StartListening()
    {
        IsListening = true;
        Transcript  = "";
        Answer      = "";
        ShowAnswer  = false;
        ShowFiller  = false;
        StatusText  = "Listening...";
        _silenceDetector.Reset();

        _speechRec.Start(partial =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Transcript = partial;
                StatusText = string.IsNullOrEmpty(partial) ? "Listening..." : partial;
            });
        });

        if (AutoListen)
        {
            _silenceDetector.OnSilence = () =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (!IsListening) return;
                    StopListening();
                    _ = GetAnswerAsync();
                });
            };
        }

        _audioCapture.OnBuffer = samples =>
        {
            _speechRec.AppendSamples(samples);
            if (AutoListen) _silenceDetector.Process(samples);
        };
        _audioCapture.Start();
    }

    public void StopListening()
    {
        IsListening = false;
        _silenceDetector.OnSilence = null;

        var final = _speechRec.Stop();
        if (!string.IsNullOrEmpty(final)) Transcript = final;

        _audioCapture.Stop();
        StatusText = string.IsNullOrEmpty(Transcript) ? "Ready" : Transcript;
    }

    // ── Get Answer ────────────────────────────────────────────────────────────
    public async Task GetAnswerAsync()
    {
        if (BedrockSvc == null && OpenRouterSvc == null && OpenAISvc == null) { StatusText = "Add API key in .env"; return; }
        if (string.IsNullOrEmpty(Transcript))                  { StatusText = "Nothing heard yet";  return; }

        IsLoadingAnswer = true;
        ShowAnswer      = true;
        Answer          = "";
        _whisperCts?.Cancel();

        SetRandomFiller();
        StatusText = "Thinking...";

        try
        {
            var full = await CollectFullAsync();
            ShowFiller = false;
            FillerText = "";

            if (WhisperMode)
                await RevealWhisperAsync(full);
            else
                Answer = full;

            IsLoadingAnswer = false;
            StatusText = "Done";

            if (FollowUpMode)
                App.Current.Dispatcher.Invoke(() => History.Add(new ConversationTurn(Transcript, full)));
        }
        catch (Exception ex)
        {
            ShowFiller      = false;
            IsLoadingAnswer = false;
            Answer          = $"Error: {ex.Message}";
            StatusText      = "Error";
        }
    }

    // ── Screenshot ────────────────────────────────────────────────────────────
    public async Task CaptureAndAnalyzeAsync()
    {
        if (BedrockSvc == null && OpenRouterSvc == null && OpenAISvc == null) { StatusText = "Add API key in .env"; return; }

        IsCapturing     = true;
        IsLoadingAnswer = true;
        ShowAnswer      = true;
        Answer          = "";
        _whisperCts?.Cancel();
        ShowFiller = false;

        try
        {
            var imageBytes = await Task.Run(ScreenshotCapture.Capture);
            StatusText = "Analyzing...";
            SetRandomFiller();

            var full = "";
            var hist = FollowUpMode ? (IEnumerable<ConversationTurn>)History : [];

            // 1. Try Bedrock
            if (BedrockSvc is { } bedrock)
            {
                try
                {
                    await foreach (var chunk in bedrock.StreamVision(imageBytes, JD, Resume, hist))
                        full += chunk;
                }
                catch
                {
                    full = "";
                    StatusText = "Bedrock failed, trying fallback...";
                    full = await VisionFallbackAsync(imageBytes, hist);
                }
            }
            else
            {
                full = await VisionFallbackAsync(imageBytes, hist);
            }

            ShowFiller = false;
            FillerText = "";

            if (WhisperMode)
                await RevealWhisperAsync(full);
            else
                Answer = full;

            if (FollowUpMode)
                App.Current.Dispatcher.Invoke(() => History.Add(new ConversationTurn("[screenshot]", full)));

            IsCapturing     = false;
            IsLoadingAnswer = false;
            StatusText      = "Done";
        }
        catch (Exception ex)
        {
            IsCapturing     = false;
            IsLoadingAnswer = false;
            ShowFiller      = false;
            Answer          = $"Error: {ex.Message}";
            StatusText      = "Capture failed";
        }
    }

    // ── Clear ─────────────────────────────────────────────────────────────────
    public void Clear()
    {
        if (IsListening) StopListening();
        _whisperCts?.Cancel();
        Transcript = "";
        Answer     = "";
        ShowAnswer = false;
        ShowFiller = false;
        FillerText = "";
        StatusText = "Ready";
        if (!FollowUpMode) History.Clear();
    }

    public void ClearHistory() => History.Clear();

    // ── Settings persistence ──────────────────────────────────────────────────
    public void SaveSettings()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { JD, Resume });
        File.WriteAllText(SettingsPath, json);
    }

    private void LoadSettings()
    {
        if (!File.Exists(SettingsPath)) return;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(SettingsPath));
            JD     = doc.RootElement.GetProperty("JD").GetString()     ?? "";
            Resume = doc.RootElement.GetProperty("Resume").GetString() ?? "";
        }
        catch { }
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private async Task<string> CollectFullAsync()
    {
        var hist = FollowUpMode ? (IEnumerable<ConversationTurn>)History : [];
        Exception? lastEx = null;

        // 1. Bedrock (Meta Llama 3.3 70B — fastest + most capable)
        if (BedrockSvc is { } bedrock)
        {
            try
            {
                var full = "";
                await foreach (var chunk in bedrock.Stream(Transcript, JD, Resume, hist))
                    full += chunk;
                return full;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                StatusText = "Bedrock failed, trying OpenRouter...";
            }
        }

        // 2. OpenRouter
        if (OpenRouterSvc is { } openRouter)
        {
            try
            {
                var full = "";
                await foreach (var chunk in openRouter.Stream(Transcript, JD, Resume, hist))
                    full += chunk;
                return full;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                StatusText = "OpenRouter failed, trying OpenAI...";
            }
        }

        // 3. OpenAI
        if (OpenAISvc is { } openAI)
        {
            var full = "";
            await foreach (var chunk in openAI.Stream(Transcript, JD, Resume, hist))
                full += chunk;
            return full;
        }

        throw lastEx ?? new InvalidOperationException("No API service available");
    }

    private async Task<string> VisionFallbackAsync(byte[] imageBytes, IEnumerable<ConversationTurn> hist)
    {
        Exception? lastEx = null;

        if (OpenRouterSvc is { } openRouter)
        {
            try
            {
                var full = "";
                await foreach (var chunk in openRouter.StreamVision(imageBytes, JD, Resume, hist))
                    full += chunk;
                return full;
            }
            catch (Exception ex) { lastEx = ex; }
        }

        if (OpenAISvc is { } openAI)
        {
            var full = "";
            await foreach (var chunk in openAI.StreamVision(imageBytes, JD, Resume, hist))
                full += chunk;
            return full;
        }

        throw lastEx ?? new InvalidOperationException("No API service available");
    }

    private async Task RevealWhisperAsync(string full)
    {
        _whisperCts = new CancellationTokenSource();
        var ct = _whisperCts.Token;
        var lines = full.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var revealed = "";

        foreach (var line in lines)
        {
            if (ct.IsCancellationRequested) return;
            revealed += (string.IsNullOrEmpty(revealed) ? "" : "\n") + line;
            App.Current.Dispatcher.Invoke(() => Answer = revealed);
            await Task.Delay(600, ct).ConfigureAwait(false);
        }
    }

    private void SetRandomFiller()
    {
        FillerText = FillerPhrases[Random.Shared.Next(FillerPhrases.Length)];
        ShowFiller = true;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
