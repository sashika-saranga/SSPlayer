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
        private BassTrebleSampleProvider? bassTreble;
        private EchoSampleProvider? echo;
        private ReverbSampleProvider? reverb;
        private StereoWidenSampleProvider? stereo;
        private SampleAggregator? aggregator;
        private readonly object lockObj = new object();

        public float[] EqGains { get; private set; } = new float[15];
        public event EventHandler<FrameEventArgs>? SampleFramesAvailable;
        // forwards per-channel peak levels (from MeteringSampleProvider.StreamVolume)
        public event EventHandler<float[]>? StreamVolumeAvailable;
        public event EventHandler<bool>? ClippingChanged;
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

            // add bass/treble processing after EQ
            bassTreble = new BassTrebleSampleProvider(equalizer);
            bassTreble.SetBassLevel(bassLevel);
            bassTreble.EnableBass(bassEnabled);
            bassTreble.SetTrebleLevel(trebleLevel);
            bassTreble.EnableTreble(trebleEnabled);

            // add echo and reverb in chain
            echo = new EchoSampleProvider(bassTreble, echoLevel);
            reverb = new ReverbSampleProvider(echo, reverbLevel);
            stereo = new StereoWidenSampleProvider(reverb, stereoLevel);

            // aggregator computes FFT and raises mapped band levels
            aggregator = new SampleAggregator(stereo, 2048);
            aggregator.FftCalculated += (s, a) => SampleFramesAvailable?.Invoke(this, a);

            // add metering so callers can observe peak levels if needed
            var metering = new MeteringSampleProvider(aggregator);
            metering.StreamVolume += (s, a) =>
            {
                try
                {
                    StreamVolumeAvailable?.Invoke(this, a.MaxSampleValues ?? new float[0]);
                }
                catch { }
            };

            // wrap with clipping detector and forward clipping events
            var clipDetector = new ClippingDetectorSampleProvider(metering);
            clipDetector.ClippingChanged += (s, isClipping) => { try { ClippingChanged?.Invoke(this, isClipping); } catch { } };

            finalSampleProvider = clipDetector;

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

        private float echoLevel = 0f;
        private float reverbLevel = 0f;
        private float stereoLevel = 0f;
        private float bassLevel = 0f;
        private float trebleLevel = 0f;
        private bool bassEnabled = false;
        private bool trebleEnabled = false;
        private bool echoEnabled = false;
        private bool reverbEnabled = false;
        private bool stereoEnabled = false;

        public void UpdateEchoLevel(float level)
        {
            echoLevel = level;
            if (echo != null)
            {
                echo.SetLevel(level);
            }
        }

        public void UpdateBassLevel(float level)
        {
            bassLevel = level;
            if (bassTreble != null)
            {
                bassTreble.SetBassLevel(level);
            }
        }

        public void UpdateTrebleLevel(float level)
        {
            trebleLevel = level;
            if (bassTreble != null)
            {
                bassTreble.SetTrebleLevel(level);
            }
        }

        public void UpdateReverbLevel(float level)
        {
            reverbLevel = level;
            if (reverb != null)
            {
                reverb.SetLevel(level);
            }
        }

        public void UpdateStereoLevel(float level)
        {
            stereoLevel = level;
            if (stereo != null)
            {
                stereo.SetLevel(level);
            }
        }

        public void EnableEcho(bool enable)
        {
            echoEnabled = enable;
            if (echo != null)
            {
                echo.SetLevel(enable ? echoLevel : 0f);
            }
        }

        public void EnableReverb(bool enable)
        {
            reverbEnabled = enable;
            if (reverb != null)
            {
                reverb.SetLevel(enable ? reverbLevel : 0f);
            }
        }

        public void EnableBass(bool enable)
        {
            bassEnabled = enable;
            if (bassTreble != null)
            {
                bassTreble.EnableBass(enable);
            }
        }

        public void EnableTreble(bool enable)
        {
            trebleEnabled = enable;
            if (bassTreble != null)
            {
                bassTreble.EnableTreble(enable);
            }
        }

        public void EnableStereo(bool enable)
        {
            stereoEnabled = enable;
            if (stereo != null)
            {
                stereo.SetLevel(enable ? stereoLevel : 0f);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
