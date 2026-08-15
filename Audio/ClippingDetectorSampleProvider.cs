using System;
using NAudio.Wave;

namespace Mp3Player.Audio
{
    // Wraps an ISampleProvider and detects clipping (samples near or exceeding +/-1.0)
    public class ClippingDetectorSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private bool lastClipping = false;

        public ClippingDetectorSampleProvider(ISampleProvider src)
        {
            source = src ?? throw new ArgumentNullException(nameof(src));
            WaveFormat = src.WaveFormat;
        }

        public WaveFormat WaveFormat { get; }

        // raised when clipping state changes; true = clipping detected
        public event EventHandler<bool>? ClippingChanged;

        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            bool clipping = false;
            for (int i = 0; i < read; i++)
            {
                if (Math.Abs(buffer[offset + i]) >= 0.999f)
                {
                    clipping = true;
                    break;
                }
            }
            if (clipping != lastClipping)
            {
                lastClipping = clipping;
                try { ClippingChanged?.Invoke(this, clipping); } catch { }
            }
            return read;
        }
    }
}
