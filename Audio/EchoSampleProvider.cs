using System;
using NAudio.Wave;

namespace Mp3Player.Audio
{
    // Simple single-tap echo (delay) sample provider
    public class EchoSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly float[] buffer;
        private int bufferPos;
        private readonly int delaySamples;
        private float level;

        public EchoSampleProvider(ISampleProvider source, float initialLevel = 0f, int delayMs = 400)
        {
            this.source = source;
            this.WaveFormat = source.WaveFormat;
            int maxDelaySeconds = 2; // allow up to 2s buffer
            buffer = new float[WaveFormat.SampleRate * WaveFormat.Channels * maxDelaySeconds];
            bufferPos = 0;
            delaySamples = (int)(WaveFormat.SampleRate * delayMs / 1000.0) * WaveFormat.Channels;
            level = initialLevel;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] bufferOut, int offset, int count)
        {
            int samplesRead = source.Read(bufferOut, offset, count);
            int channels = WaveFormat.Channels;
            for (int n = 0; n < samplesRead; n++)
            {
                int bufIndex = (bufferPos + n) % buffer.Length;
                float delayed = buffer[bufIndex];
                float dry = bufferOut[offset + n];
                float wet = delayed * level;
                float outSample = dry + wet;
                // clamp to avoid extreme values
                if (outSample > 1f) outSample = 1f;
                if (outSample < -1f) outSample = -1f;
                bufferOut[offset + n] = outSample;

                // write current sample into delay buffer (simple feedback-free echo)
                buffer[bufIndex] = dry + delayed * 0.5f; // minor feedback to sustain
            }
            bufferPos = (bufferPos + samplesRead) % buffer.Length;
            return samplesRead;
        }

        public void SetLevel(float newLevel)
        {
            level = newLevel;
        }
    }
}
