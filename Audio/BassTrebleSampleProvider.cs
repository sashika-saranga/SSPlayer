using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace Mp3Player.Audio
{
    // Simple bass/treble boost provider using low/high shelf filters and a mild exciter on highs.
    public class BassTrebleSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly BiQuadFilter[] lowShelves;
        private readonly BiQuadFilter[] highShelves;
        private readonly BiQuadFilter[] highPassForExciter;
        // band-split filters for subharmonic generation
        private readonly BiQuadFilter[] bandLowPass;
        private readonly BiQuadFilter[] smoothLowPass;
        private readonly BiQuadFilter[] dcHighPass;
        private float bassAmount = 0f; // 0..1
        private float trebleAmount = 0f; // 0..1
        private bool bassEnabled = false;
        private bool trebleEnabled = false;
        private float subMixScale = 0.6f;

        public BassTrebleSampleProvider(ISampleProvider src)
        {
            source = src ?? throw new ArgumentNullException(nameof(src));
            WaveFormat = src.WaveFormat;
            int ch = WaveFormat.Channels;
            lowShelves = new BiQuadFilter[ch];
            highShelves = new BiQuadFilter[ch];
            highPassForExciter = new BiQuadFilter[ch];
            bandLowPass = new BiQuadFilter[ch];
            smoothLowPass = new BiQuadFilter[ch];
            dcHighPass = new BiQuadFilter[ch];
            CreateFilters();
        }

        private void CreateFilters()
        {
            int sampleRate = WaveFormat.SampleRate;
            for (int c = 0; c < WaveFormat.Channels; c++)
            {
                // default shelf settings; gain will be applied in Update methods
                lowShelves[c] = BiQuadFilter.LowShelf(sampleRate, 120f, 0.7f, 0f);
                highShelves[c] = BiQuadFilter.HighShelf(sampleRate, 6000f, 0.7f, 0f);
                highPassForExciter[c] = BiQuadFilter.HighPassFilter(sampleRate, 3000f, 0.7f);
                // filters used to extract low band and synthesize subharmonic content
                bandLowPass[c] = BiQuadFilter.LowPassFilter(sampleRate, 120f, 0.7f);
                smoothLowPass[c] = BiQuadFilter.LowPassFilter(sampleRate, 120f, 0.7f);
                dcHighPass[c] = BiQuadFilter.HighPassFilter(sampleRate, 30f, 0.7f);
            }
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int samples = source.Read(buffer, offset, count);
            if (samples == 0) return 0;
            int channels = WaveFormat.Channels;
            for (int n = 0; n < samples; n += channels)
            {
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = offset + n + ch;
                    float input = buffer[idx];
                    float processed = input;

                    // Apply bass shelf mixing
                    if (bassEnabled && bassAmount > 0f)
                    {
                        // low shelf contribution
                        float low = lowShelves[ch].Transform(input);
                        // subharmonic generation: extract low band, rectify, smooth and remove DC
                        float band = bandLowPass[ch].Transform(input);
                        float rect = Math.Abs(band);
                        float smooth = smoothLowPass[ch].Transform(rect);
                        float sub = dcHighPass[ch].Transform(smooth);
                        // mix original, shelf and subharmonic
                        processed = processed * (1f - bassAmount) + low * bassAmount + sub * (bassAmount * subMixScale);
                    }

                    // Apply treble shelf and mild exciter
                    if (trebleEnabled && trebleAmount > 0f)
                    {
                        float high = highShelves[ch].Transform(input);
                        // exciter: extract high band then apply soft waveshaper
                        float hp = highPassForExciter[ch].Transform(input);
                        float drive = 1f + trebleAmount * 3f;
                        float exc = (float)Math.Tanh(hp * drive);
                        // mix original processed, shelf and exciter
                        processed = processed * (1f - trebleAmount) + high * trebleAmount + exc * (trebleAmount * 0.35f);
                    }

                    buffer[idx] = processed;
                }
            }
            return samples;
        }

        public void SetBassLevel(float level)
        {
            bassAmount = Math.Max(0f, Math.Min(1f, level));
            // map amount to dB gain range 0..12 dB
            float gainDb = bassAmount * 12f;
            for (int c = 0; c < lowShelves.Length; c++)
            {
                lowShelves[c] = BiQuadFilter.LowShelf(WaveFormat.SampleRate, 120f, 0.7f, gainDb);
            }
            // adjust submix scaling slightly to correlate with level
            subMixScale = 0.4f + 0.6f * bassAmount; // between 0.4..1.0
        }

        public void EnableBass(bool enable)
        {
            bassEnabled = enable;
        }

        public void SetTrebleLevel(float level)
        {
            trebleAmount = Math.Max(0f, Math.Min(1f, level));
            float gainDb = trebleAmount * 12f;
            for (int c = 0; c < highShelves.Length; c++)
            {
                highShelves[c] = BiQuadFilter.HighShelf(WaveFormat.SampleRate, 6000f, 0.7f, gainDb);
            }
        }

        public void EnableTreble(bool enable)
        {
            trebleEnabled = enable;
        }
    }
}
