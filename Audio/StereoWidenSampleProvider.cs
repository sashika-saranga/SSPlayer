using System;
using NAudio.Wave;

namespace Mp3Player.Audio
{
    // Simple stereo widening using mid/side processing.
    public class StereoWidenSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private float level; // 0 = center, 1 = original, >1 widen

        public StereoWidenSampleProvider(ISampleProvider source, float initialLevel = 0f)
        {
            this.source = source;
            this.WaveFormat = source.WaveFormat;
            this.level = initialLevel;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            int channels = WaveFormat.Channels;
            if (channels < 2 || level == 0f)
            {
                // nothing to do
                return samplesRead;
            }

            // process in blocks of channels to minimize index math
            for (int n = 0; n < samplesRead; n += channels)
            {
                float l = buffer[offset + n];
                float r = buffer[offset + n + 1];
                // mid/side
                float mid = (l + r) * 0.5f;
                float side = (l - r) * 0.5f;
                // increase side by level factor (1.0 = unchanged, >1 widen)
                float newSide = side * level;
                float newL = mid + newSide;
                float newR = mid - newSide;
                // clamp
                if (newL > 1f) newL = 1f; if (newL < -1f) newL = -1f;
                if (newR > 1f) newR = 1f; if (newR < -1f) newR = -1f;
                buffer[offset + n] = newL;
                buffer[offset + n + 1] = newR;
            }

            return samplesRead;
        }

        public void SetLevel(float newLevel)
        {
            // ensure non-negative
            level = Math.Max(0f, newLevel);
        }
    }
}
