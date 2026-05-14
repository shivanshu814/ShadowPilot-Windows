using System.Speech.Recognition;

namespace ShadowPilot.Services;

// Uses Windows built-in speech recognition (System.Speech) — no extra API keys needed
public sealed class SpeechRecognizerService : IDisposable
{
    private SpeechRecognitionEngine? _engine;
    private Action<string>? _onPartial;
    private string _accumulated = "";

    public void Start(Action<string> onPartial)
    {
        _onPartial = onPartial;
        _accumulated = "";

        _engine = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"));
        _engine.LoadGrammar(new DictationGrammar());
        _engine.SetInputToDefaultAudioDevice();

        _engine.SpeechRecognized += (_, e) =>
        {
            var text = e.Result.Text;
            _accumulated += (string.IsNullOrEmpty(_accumulated) ? "" : " ") + text;
            onPartial(_accumulated);
        };

        _engine.SpeechHypothesized += (_, e) =>
        {
            var hypothesis = _accumulated +
                (string.IsNullOrEmpty(_accumulated) ? "" : " ") + e.Result.Text;
            onPartial(hypothesis);
        };

        _engine.RecognizeAsync(RecognizeMode.Multiple);
    }

    // Feed float samples from SystemAudioCapture to the speech engine
    // Note: System.Speech uses the default audio device directly, so this is a no-op.
    // For full loopback-to-speech support, wire a virtual audio device or
    // switch input to mic. This service listens on the default input (mic).
    public void AppendSamples(float[] _) { /* System.Speech uses device input directly */ }

    public string Stop()
    {
        _engine?.RecognizeAsyncStop();
        _engine?.Dispose();
        _engine = null;
        return _accumulated;
    }

    public void Dispose() => Stop();
}
