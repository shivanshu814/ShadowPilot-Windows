namespace ShadowPilot.Services;

// A bounded producer/consumer stream. An empty live queue is NOT end-of-stream.
internal sealed class PcmAudioStream(int capacityBytes) : Stream
{
    private readonly object _gate = new();
    private readonly byte[] _buffer = new byte[capacityBytes];
    private int _head, _count;
    private long _position;
    private bool _completed;

    public void AppendSamples(float[] samples)
    {
        lock (_gate)
        {
            if (_completed) return; // A capture callback may already be in flight at stop.
            if (samples.Length > (_buffer.Length - _count) / 2)
                throw new IOException("Speech recognition cannot keep up with system audio.");
            foreach (var sample in samples)
            {
                var value = float.IsNaN(sample) ? 0 : Math.Clamp(sample, -1f, 1f);
                var pcm = (short)Math.Clamp((int)MathF.Round(value * 32768f), short.MinValue, short.MaxValue);
                _buffer[(_head + _count++) % _buffer.Length] = (byte)pcm;
                _buffer[(_head + _count++) % _buffer.Length] = (byte)(pcm >> 8);
            }
            Monitor.PulseAll(_gate);
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            _completed = true;
            Monitor.PulseAll(_gate);
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Invalid buffer range.");
        if (count == 0) return 0;
        lock (_gate)
        {
            var total = 0;
            while (total < count)
            {
                while (_count == 0 && !_completed) Monitor.Wait(_gate);
                if (_count == 0) break;
                var read = Math.Min(count - total, _count);
                for (var i = 0; i < read; i++) buffer[offset + total + i] = _buffer[(_head + i) % _buffer.Length];
                _head = (_head + read) % _buffer.Length;
                _count -= read;
                _position += read;
                total += read;
            }
            return total;
        }
    }

    protected override void Dispose(bool disposing) { Complete(); base.Dispose(disposing); }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    // System.Speech's COM wrapper reads Length even for live streams and caches it.
    // Advertise an open-ended stream; only Complete/Dispose causes Read to return EOF.
    public override long Length => long.MaxValue;
    public override long Position { get { lock (_gate) return _position; } set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin)
    {
        lock (_gate)
        {
            // SAPI queries the current position; consumed live audio cannot be rewound.
            if ((origin == SeekOrigin.Current && offset == 0) ||
                (origin == SeekOrigin.Begin && offset == _position)) return _position;
            throw new NotSupportedException("Live audio cannot be rewound.");
        }
    }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
