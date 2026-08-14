using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Dsp;

namespace Mp3Player.Audio
{
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer? outputDevice;
        private AudioFileReader? audioFileReader;
        private ISampleProvider? finalSampleProvider;
        private EqualizerSampleProvider? equalizer;
        private SampleAggregator? aggregator;
        private readonly object lockObj = new object();

        public float[] EqGains { get; private set; } = new float[15];

        public event EventHandler<FrameEventArgs>? SampleFramesAvailable;
        // Raised when playback naturally reaches the end of a file
        public event EventHandler? PlaybackEnded;

        public TimeSpan CurrentTime => audioFileReader?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => audioFileReader?.TotalTime ?? TimeSpan.Zero;
        public bool IsPlaying { get; private set; }

        public void PlayFile(string path, float volume)
        {
            Stop();

            audioFileReader = new AudioFileReader(path) { Volume = volume };

            // build processing chain: equalizer -> aggregator (FFT) -> metering -> output
            equalizer = new EqualizerSampleProvider(audioFileReader.ToSampleProvider(), EqGains);

            // aggregator computes FFT and raises mapped band levels
            aggregator = new SampleAggregator(equalizer, 2048);
            aggregator.FftCalculated += (s, a) => SampleFramesAvailable?.Invoke(this, a);

            // add metering so callers can observe peak levels if needed
            var metering = new MeteringSampleProvider(aggregator);
            metering.StreamVolume += (s, a) =>
            {
                // keep peak values as well (not used for band mapping)
                // ignore here to avoid duplicate events
            };

            finalSampleProvider = metering;

            outputDevice = new WaveOutEvent();
            outputDevice.Init(finalSampleProvider.ToWaveProvider());
            // hook playback stopped to detect natural end
            outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
            outputDevice.Play();
            IsPlaying = true;
        }

        private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // mark not playing
            IsPlaying = false;

            try
            {
                // determine if playback naturally reached end
                if (audioFileReader != null)
                {
                    // if position is at or beyond length assume natural end
                    if (audioFileReader.Position >= audioFileReader.Length)
                    {
                        PlaybackEnded?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch { }
        }

        public void SetVolume(float volume)
        {
            if (audioFileReader != null)
                audioFileReader.Volume = volume;
        }

        public void Seek(TimeSpan position)
        {
            if (audioFileReader == null) return;
            // clamp
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;
            if (position > audioFileReader.TotalTime) position = audioFileReader.TotalTime;
            audioFileReader.CurrentTime = position;
        }

        public void Pause()
        {
            if (outputDevice != null)
            {
                outputDevice.Pause();
                IsPlaying = false;
            }
        }

        public void Resume()
        {
            if (outputDevice != null)
            {
                outputDevice.Play();
                IsPlaying = true;
            }
        }

        public void Stop()
        {
            lock (lockObj)
            {
                if (outputDevice != null)
                {
                    // unsubscribe handler then stop/dispose
                    try { outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped; } catch { }
                    outputDevice.Stop();
                    outputDevice.Dispose();
                    outputDevice = null;
                }
                if (audioFileReader != null)
                {
                    audioFileReader.Dispose();
                    audioFileReader = null;
                }
            }
            IsPlaying = false;
        }

        public void UpdateEqGains(float[] gains)
        {
            EqGains = (float[])gains.Clone();
            if (equalizer != null)
            {
                equalizer.UpdateGains(EqGains);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
