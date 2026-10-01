using System.Runtime.Versioning;

namespace ShadowPilot.Services;

public sealed class SpeechRecognizerService : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<IRecognitionEngine> _createEngine;
    private readonly TimeSpan _stopTimeout;
    private Session? _session;
    private bool _disposed;

    private sealed class Session(IRecognitionEngine engine, PcmAudioStream audio, Action<string> onPartial)
    {
        public readonly IRecognitionEngine Engine = engine;
        public readonly PcmAudioStream Audio = audio;
        public readonly Action<string> OnPartial = onPartial;
        public readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Text = "";
        public Task<string>? StopTask;
    }

    [SupportedOSPlatform("windows")]
    public SpeechRecognizerService() : this(() => new WindowsRecognitionEngine(), TimeSpan.FromSeconds(5)) { }
    internal SpeechRecognizerService(Func<IRecognitionEngine> createEngine, TimeSpan? stopTimeout = null)
    {
        _createEngine = createEngine;
        _stopTimeout = stopTimeout ?? TimeSpan.FromSeconds(5);
    }

    public Action<float[]> Start(int sampleRate, Action<string> onPartial, Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(onPartial);
        ArgumentNullException.ThrowIfNull(onError);
        if (sampleRate < 8000 || sampleRate > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session != null) throw new InvalidOperationException("Recognition is already active or stopping.");
            var session = new Session(_createEngine(), new PcmAudioStream(sampleRate * 2 * 10), onPartial);
            _session = session;
            session.Engine.Recognized += text => Publish(session, text, final: true);
            session.Engine.Hypothesized += text => Publish(session, text, final: false);
            session.Engine.Completed += error =>
            {
                bool unexpected;
                lock (_gate) unexpected = _session == session && session.StopTask == null;
                if (error == null) session.Completion.TrySetResult();
                else session.Completion.TrySetException(error);
                if (unexpected) onError(error ?? new IOException("System audio recognition ended unexpectedly."));
            };
            try
            {
                session.Engine.Start(session.Audio, sampleRate);
                // Bind capture to this stream so late callbacks can never feed a new session.
                return session.Audio.AppendSamples;
            }
            catch
            {
                _session = null;
                session.Audio.Complete();
                try { session.Engine.Dispose(); } finally { session.Audio.Dispose(); }
                throw;
            }
        }
    }

    private void Publish(Session session, string text, bool final)
    {
        string transcript;
        lock (_gate)
        {
            if (_session != session) return;
            transcript = session.Text + (session.Text.Length == 0 ? "" : " ") + text;
            if (final) session.Text = transcript;
        }
        session.OnPartial(transcript);
    }

    public void AppendSamples(float[] samples)
    {
        lock (_gate) _session?.Audio.AppendSamples(samples);
    }

    public Task<string> StopAsync()
    {
        lock (_gate)
        {
            if (_session == null) return Task.FromResult("");
            return _session.StopTask ??= StopSessionAsync(_session);
        }
    }

    private async Task<string> StopSessionAsync(Session session)
    {
        // Let StopTask be assigned before EOF can trigger RecognizeCompleted.
        await Task.Yield();
        session.Audio.Complete();
        try
        {
            // EOF drains queued audio and finalizes the last utterance before returning text.
            await session.Completion.Task.WaitAsync(_stopTimeout).ConfigureAwait(false);
            lock (_gate) return session.Text;
        }
        finally
        {
            bool ownsSession;
            lock (_gate)
            {
                ownsSession = _session == session;
                if (ownsSession) _session = null;
            }
            if (ownsSession)
            {
                try
                {
                    if (!session.Completion.Task.IsCompleted) session.Engine.Cancel();
                }
                finally
                {
                    try { session.Engine.Dispose(); } finally { session.Audio.Dispose(); }
                }
            }
        }
    }

    public void Dispose()
    {
        Session? session;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            session = _session;
            _session = null;
        }
        if (session == null) return;
        session.Audio.Complete(); // Release a blocked native reader before disposing the engine.
        session.Completion.TrySetCanceled();
        try { session.Engine.Cancel(); }
        finally { try { session.Engine.Dispose(); } finally { session.Audio.Dispose(); } }
    }
}
