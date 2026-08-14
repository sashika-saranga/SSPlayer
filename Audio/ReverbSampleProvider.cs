using System;
using NAudio.Wave;

namespace Mp3Player.Audio
{
    // Simple multi-tap reverb-like effect. Not a high-quality reverb but usable for demo.
    public class ReverbSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly float[] buffer;
        private int bufferPos;
        private readonly int[] tapDelays; // in samples
        private readonly float[] tapDecays;
        private float level;

        public ReverbSampleProvider(ISampleProvider source, float initialLevel = 0f)
        {
            this.source = source;
            this.WaveFormat = source.WaveFormat;
            int maxDelaySeconds = 2;
            buffer = new float[WaveFormat.SampleRate * WaveFormat.Channels * maxDelaySeconds];
            bufferPos = 0;
            // small set of taps to simulate reverb tail (ms)
            int sr = WaveFormat.SampleRate;
            // delays per channel (interleaved), we'll compute by samples including channels
            var delaysMs = new int[] { 50, 73, 97, 120 };
            tapDelays = new int[delaysMs.Length * WaveFormat.Channels];
            for (int i = 0; i < delaysMs.Length; i++)
            {
                int dSamples = (int)(sr * delaysMs[i] / 1000.0);
                for (int ch = 0; ch < WaveFormat.Channels; ch++)
                {
                    tapDelays[i * WaveFormat.Channels + ch] = dSamples * WaveFormat.Channels + ch;
                }
            }
            tapDecays = new float[] { 0.6f, 0.5f, 0.4f, 0.3f };
            level = initialLevel;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] bufferOut, int offset, int count)
        {
            int samplesRead = source.Read(bufferOut, offset, count);
            int channels = WaveFormat.Channels;
            for (int n = 0; n < samplesRead; n++)
            {
                int outIndex = offset + n;
                float dry = bufferOut[outIndex];
                float wetSum = 0f;
                // sum taps
                for (int t = 0; t < tapDecays.Length; t++)
                {
                    int tapIndex = (bufferPos - tapDelays[t] + buffer.Length) % buffer.Length;
                    wetSum += buffer[tapIndex] * tapDecays[t];
                }
                float outSample = dry + wetSum * level;
                if (outSample > 1f) outSample = 1f;
                if (outSample < -1f) outSample = -1f;
                bufferOut[outIndex] = outSample;

                // write current sample into buffer with slight damping
                buffer[bufferPos] = dry + wetSum * 0.5f;
                bufferPos = (bufferPos + 1) % buffer.Length;
            }
            return samplesRead;
        }

        public void SetLevel(float newLevel)
        {
            level = newLevel;
        }
    }
}
