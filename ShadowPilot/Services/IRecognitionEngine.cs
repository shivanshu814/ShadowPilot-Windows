namespace ShadowPilot.Services;

// The backend can only receive explicitly supplied audio; there is no microphone fallback.
internal interface IRecognitionEngine : IDisposable
{
    event Action<string>? Recognized;
    event Action<string>? Hypothesized;
    event Action<Exception?>? Completed;
    void Start(Stream audio, int sampleRate);
    void Cancel();
}
