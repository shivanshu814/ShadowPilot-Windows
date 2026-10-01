using System.Globalization;
using System.Runtime.Versioning;
using System.Speech.AudioFormat;
using System.Speech.Recognition;

namespace ShadowPilot.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsRecognitionEngine : IRecognitionEngine
{
    private readonly SpeechRecognitionEngine _engine = new(new CultureInfo("en-US"));
    public event Action<string>? Recognized;
    public event Action<string>? Hypothesized;
    public event Action<Exception?>? Completed;

    public WindowsRecognitionEngine()
    {
        _engine.SpeechRecognized += (_, e) => Recognized?.Invoke(e.Result.Text);
        _engine.SpeechHypothesized += (_, e) => Hypothesized?.Invoke(e.Result.Text);
        _engine.RecognizeCompleted += (_, e) => Completed?.Invoke(e.Error);
    }

    public void Start(Stream audio, int sampleRate)
    {
        _engine.LoadGrammar(new DictationGrammar());
        _engine.SetInputToAudioStream(audio,
            new SpeechAudioFormatInfo(sampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
        _engine.RecognizeAsync(RecognizeMode.Multiple);
    }

    public void Cancel() => _engine.RecognizeAsyncCancel();
    public void Dispose() => _engine.Dispose();
}
