using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace Mp3Player.Audio
{
    // Wraps an ISampleProvider, passes samples through and computes FFT-based band levels
    public class SampleAggregator : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int fftLength;
        private readonly float[] fftBuffer;
        private int fftPos;

        public event EventHandler<FrameEventArgs>? FftCalculated;

        public SampleAggregator(ISampleProvider source, int fftLength = 2048)
        {
            if ((fftLength & (fftLength - 1)) != 0) throw new ArgumentException("fftLength must be power of two");
            this.source = source;
            this.fftLength = fftLength;
            fftBuffer = new float[fftLength];
            this.WaveFormat = source.WaveFormat;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);
            int channels = WaveFormat.Channels;
            for (int n = 0; n < samplesRead; n += channels)
            {
                // mix down to mono for FFT
                float sample = 0f;
                for (int ch = 0; ch < channels; ch++)
                {
                    sample += buffer[offset + n + ch];
                }
                sample /= channels;

                fftBuffer[fftPos++] = sample;
                if (fftPos >= fftLength)
                {
                    // perform FFT
                    var complex = new Complex[fftLength];
                    for (int i = 0; i < fftLength; i++)
                    {
                        // apply Hamming window
                        double window = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (fftLength - 1));
                        complex[i].X = fftBuffer[i] * (float)window;
                        complex[i].Y = 0f;
                    }
                    FastFourierTransform.FFT(true, (int)Math.Log(fftLength, 2), complex);

                    int half = fftLength / 2;
                    double[] magnitudes = new double[half];
                    for (int i = 0; i < half; i++)
                    {
                        double re = complex[i].X;
                        double im = complex[i].Y;
                        magnitudes[i] = Math.Sqrt(re * re + im * im);
                    }

                    // map magnitudes to the 31 bands defined in Equalizer.Frequencies
                    var bandCenters = EqualizerSampleProvider.Frequencies;
                    int bands = bandCenters.Length;
                    float[] bandLevels = new float[bands];

                    double sampleRate = WaveFormat.SampleRate;
                    // compute band boundaries using geometric mean
                    double[] lower = new double[bands];
                    double[] upper = new double[bands];
                    for (int b = 0; b < bands; b++)
                    {
                        double center = bandCenters[b];
                        double prev = b == 0 ? center / Math.Sqrt(2) : bandCenters[b - 1];
                        double next = b == bands - 1 ? center * Math.Sqrt(2) : bandCenters[b + 1];
                        lower[b] = Math.Sqrt(prev * center);
                        upper[b] = Math.Sqrt(center * next);
                    }

                    // accumulate bin magnitudes into bands
                    for (int k = 0; k < half; k++)
                    {
                        double freq = k * sampleRate / fftLength;
                        double mag = magnitudes[k];
                        for (int b = 0; b < bands; b++)
                        {
                            if (freq >= lower[b] && freq < upper[b])
                            {
                                bandLevels[b] += (float)mag;
                                break;
                            }
                        }
                    }

                    // normalize and convert to 0..1 (log-like scaling)
                    float max = 0f;
                    for (int b = 0; b < bands; b++) if (bandLevels[b] > max) max = bandLevels[b];
                    var outLevels = new float[bands];
                    for (int b = 0; b < bands; b++)
                    {
                        double v = bandLevels[b];
                        if (max > 0) v /= max;
                        // apply slight compression
                        outLevels[b] = (float)Math.Min(1.0, Math.Log10(1 + 9 * v));
                    }

                    FftCalculated?.Invoke(this, new FrameEventArgs { Volumes = outLevels });

                    // reset
                    fftPos = 0;
                }
            }

            return samplesRead;
        }
    }
}
