using NAudio.Wave;

namespace ShadowPilot.Services;

// Captures the default playback endpoint, never the default recording device.
public sealed class SystemAudioCapture : IDisposable
{
    public Action<float[]>? OnBuffer;
    public Action<Exception>? OnError;
    private WasapiLoopbackCapture? _capture;
    private LoopbackSampleConverter? _converter;

    // Open first so the recognizer receives the actual endpoint sample rate before any samples.
    public int Prepare()
    {
        if (_capture != null) throw new InvalidOperationException("Capture is already prepared.");
        var capture = new WasapiLoopbackCapture();
        try
        {
            _converter = new LoopbackSampleConverter(capture.WaveFormat);
            capture.DataAvailable += OnData;
            capture.RecordingStopped += OnStopped;
            _capture = capture;
            return capture.WaveFormat.SampleRate;
        }
        catch { capture.Dispose(); throw; }
    }

    public void Start()
    {
        var capture = _capture ?? throw new InvalidOperationException("Prepare capture before starting.");
        try { capture.StartRecording(); }
        catch { Stop(); throw; }
    }

    public void Stop()
    {
        var capture = Interlocked.Exchange(ref _capture, null);
        if (capture == null) return;
        capture.DataAvailable -= OnData;
        capture.RecordingStopped -= OnStopped;
        try { capture.StopRecording(); } finally { capture.Dispose(); }
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var converter = _converter;
        var onBuffer = OnBuffer;
        var onError = OnError;
        if (!ReferenceEquals(sender, _capture) || converter == null || e.BytesRecorded == 0) return;
        try { onBuffer?.Invoke(converter.Convert(e.Buffer, e.BytesRecorded)); }
        catch (Exception ex) { onError?.Invoke(ex); }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        var onError = OnError;
        if (ReferenceEquals(sender, _capture))
            onError?.Invoke(e.Exception ?? new IOException("System audio capture stopped unexpectedly."));
    }

    public void Dispose() => Stop();
}
