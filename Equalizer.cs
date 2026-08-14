using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace Mp3Player.Audio
{
    // Simple 10-band equalizer sample provider using BiQuad filters
    public class EqualizerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly BiQuadFilter[,] filters; // channel x band
        private readonly float[] gains;

        public static readonly float[] Frequencies = new float[] {
            // Common 15-band graphic EQ center frequencies (Hz)
            25f, 40f, 63f, 100f, 160f,
            250f, 400f, 630f, 1000f, 1600f,
            2500f, 4000f, 6300f, 10000f, 16000f };

        public EqualizerSampleProvider(ISampleProvider source, float[] gains)
        {
            this.source = source;
            this.WaveFormat = source.WaveFormat;
            this.gains = (float[])gains.Clone();
            filters = new BiQuadFilter[WaveFormat.Channels, Frequencies.Length];
            CreateFilters();
        }

        private void CreateFilters()
        {
            for (int ch = 0; ch < WaveFormat.Channels; ch++)
            {
                for (int i = 0; i < Frequencies.Length; i++)
                {
                    filters[ch, i] = BiQuadFilter.PeakingEQ(WaveFormat.SampleRate, Frequencies[i], 1.0f, gains[i]);
                }
            }
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            if (samplesRead == 0) return 0;

            int channels = WaveFormat.Channels;
            for (int n = 0; n < samplesRead; n += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = offset + n + ch;
                    float sample = buffer[idx];
                    float processed = sample;
                    for (int b = 0; b < Frequencies.Length; b++)
                    {
                        processed = filters[ch, b].Transform(processed);
                    }
                    buffer[idx] = processed;
                }
            }

            return samplesRead;
        }

        public void UpdateGains(float[] newGains)
        {
            if (newGains.Length != gains.Length) throw new ArgumentException("gains length mismatch");
            Array.Copy(newGains, gains, gains.Length);
            // recreate filters with new gains
            for (int ch = 0; ch < WaveFormat.Channels; ch++)
            {
                for (int i = 0; i < Frequencies.Length; i++)
                {
                    filters[ch, i] = BiQuadFilter.PeakingEQ(WaveFormat.SampleRate, Frequencies[i], 1.0f, gains[i]);
                }
            }
        }
    }
}
