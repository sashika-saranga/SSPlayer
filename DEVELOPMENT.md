# Developer Notes

Purpose
- Provide contextual guidance for continuing development on the WPF MP3 player.

Project structure (key folders/files)
- Mp3Player.csproj — project file; references NAudio package.
- App.xaml / App.xaml.cs — entry for WPF app.
- MainWindow.xaml / MainWindow.xaml.cs — UI and primary interaction. Controls to wire or extend:
  - BtnSelectFolder -> folder selection and LoadTracks
  - ListTracks -> double-click to play
  - BtnPlayPause/BtnNext/BtnPrev -> playback control
  - SliderVolume -> audioFileReader.Volume (already wired)
  - SliderProgress -> currently read-only UI; seeking not implemented
  - EQ sliders (Band0..Band9) -> call player.UpdateEqGains
  - Effect checkboxes -> currently placeholders
- Audio/Equalizer.cs (EqualizerSampleProvider) — contains a 10-band equalizer implemented with NAudio.Dsp.BiQuadFilter.PeakingEQ.
- Audio/AudioPlayer.cs — manages playback using WaveOutEvent + AudioFileReader; composes the processing chain (EQ -> Metering -> Output).

Key integration points
- To add effects (echo/reverb/stereo widening): modify AudioPlayer.PlayFile to insert sample providers after the EQ and before the metering/output. Example chain:

  AudioFileReader -> EqualizerSampleProvider -> CustomEffectSampleProvider -> MeteringSampleProvider -> WaveOutEvent

- For seeking: the AudioFileReader supports setting CurrentTime. When user drags SliderProgress, calculate TimeSpan and set audioFileReader.CurrentTime. Ensure you guard against null and not-too-frequent updates.

Spectrum analyzer
- Currently uses MeteringSampleProvider.StreamVolume peak values to draw bars. For a frequency-spectrum (per-band), perform an FFT on sample buffers. Recommended approach:
  - Create a SampleAggregator that buffers samples then raises an event with float[] samples
  - Use MathNet.Numerics for FFT or implement an FFT to transform samples to frequency bins
  - Map FFT bins to UI bars and update on the UI thread

Extending EQ
- EqualizerSampleProvider.UpdateGains updates the BiQuad filters. If you want smoother changes, consider applying interpolation rather than recreating filters immediately.

Dependencies
- NAudio (already referenced). If you add FFT or DSP helpers consider adding MathNet.Numerics or a dedicated DSP library.

Testing
- Audio behavior tests are harder to automate. Aim for unit tests around helper classes (e.g., EQ filter coefficient calculations) and manual integration tests for playback.

Development tips
- Use WaveOutEvent for broad compatibility; WasapiOut is useful if you need exclusive-mode playback or advanced device handling.
- Keep audio processing off the UI thread.
- Dispose IWavePlayer and AudioFileReader to release OS audio resources when stopping or closing the app.

If you want, I can also create a TODO.md with prioritized tasks or add sample effect implementations.
