using System.Buffers.Binary;
using NAudio.Wave;

namespace ShadowPilot.Services;

internal sealed class LoopbackSampleConverter
{
    private static readonly Guid Pcm = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid Float = new("00000003-0000-0010-8000-00aa00389b71");
    private readonly WaveFormat _format;
    private readonly bool _isFloat;

    public LoopbackSampleConverter(WaveFormat format)
    {
        _format = format;
        var encoding = format.Encoding;
        if (format is WaveFormatExtensible extensible)
            encoding = extensible.SubFormat == Float ? WaveFormatEncoding.IeeeFloat
                : extensible.SubFormat == Pcm ? WaveFormatEncoding.Pcm : WaveFormatEncoding.Unknown;
        _isFloat = encoding == WaveFormatEncoding.IeeeFloat;
        if (format.Channels < 1 || (_isFloat ? format.BitsPerSample != 32
            : encoding != WaveFormatEncoding.Pcm || format.BitsPerSample is not (16 or 24 or 32)))
            throw new NotSupportedException($"Unsupported loopback audio format: {format}");
    }

    public float[] Convert(byte[] buffer, int count)
    {
        if (count < 0 || count > buffer.Length || count % _format.BlockAlign != 0)
            throw new ArgumentException("Loopback audio must contain complete frames.", nameof(count));
        var mono = new float[count / _format.BlockAlign];
        var bytesPerSample = _format.BitsPerSample / 8;
        for (var frame = 0; frame < mono.Length; frame++)
        {
            double sum = 0;
            for (var channel = 0; channel < _format.Channels; channel++)
            {
                var offset = frame * _format.BlockAlign + channel * bytesPerSample;
                var span = buffer.AsSpan(offset, bytesPerSample);
                var sample = _isFloat ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span))
                    : bytesPerSample == 2 ? BinaryPrimitives.ReadInt16LittleEndian(span) / 32768f
                    : bytesPerSample == 3 ? ((span[0] << 8 | span[1] << 16 | span[2] << 24) >> 8) / 8388608f
                    : BinaryPrimitives.ReadInt32LittleEndian(span) / 2147483648f;
                sum += float.IsFinite(sample) ? sample : 0;
            }
            mono[frame] = (float)(sum / _format.Channels);
        }
        return mono;
    }
}
