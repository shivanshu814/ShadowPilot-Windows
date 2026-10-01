namespace ShadowPilot.Services;

public sealed class SilenceDetector
{
    public Action? OnSilence;
    private readonly object _gate = new();
    private readonly double _threshold;
    private readonly Func<long> _now;
    private const float RmsFloor = 0.01f;
    private long? _lastSound;
    private bool _fired;

    public SilenceDetector(double thresholdSeconds = 1.8) : this(thresholdSeconds, () => Environment.TickCount64) { }
    internal SilenceDetector(double thresholdSeconds, Func<long> now)
    {
        _threshold = thresholdSeconds * 1000;
        _now = now;
    }

    public void Reset()
    {
        lock (_gate) { _lastSound = null; _fired = false; }
    }

    public void Process(float[] samples)
    {
        if (samples.Length == 0) return;
        double sum = 0;
        foreach (var sample in samples) sum += sample * sample;
        if (Math.Sqrt(sum / samples.Length) >= RmsFloor)
        {
            lock (_gate) _lastSound = _now();
        }
    }

    // WASAPI may emit no packets when playback is idle, so silence needs a clock too.
    public void CheckSilence()
    {
        Action? callback;
        lock (_gate)
        {
            if (_fired || !_lastSound.HasValue || _now() - _lastSound.Value < _threshold) return;
            _fired = true;
            callback = OnSilence;
        }
        callback?.Invoke();
    }
}
