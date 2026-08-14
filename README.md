# MP3 Player (WPF) — Project Context

This repository contains a simple WPF MP3 player implemented in C# using NAudio. The project was scaffolded to meet the requirements listed in Requirment.md: folder selection, track list, play/pause/prev/next controls, progress and volume controls, a spectrum analyzer, and an audio settings block with a 10-band equalizer and effect toggles.

Quick summary
- Framework: .NET 6.0 (net6.0-windows)
- UI: WPF
- Audio: NAudio (NuGet package, referenced in Mp3Player.csproj)

Implemented features
- Select folder (FolderBrowserDialog) and list MP3 files (top-level only)
- Play/Pause, Previous, Next controls
- Volume control
- 10-band equalizer implemented via BiQuadFilter (EqualizerSampleProvider)
- Simple spectrum visualization using NAudio MeteringSampleProvider peak values
- Project layout with Audio classes separated under Mp3Player/Audio

Files of interest
- Mp3Player.csproj — project file and NuGet reference
- App.xaml / App.xaml.cs — WPF application entry
- MainWindow.xaml / MainWindow.xaml.cs — main UI and wiring to AudioPlayer
- Audio/AudioPlayer.cs — playback manager (WaveOutEvent, AudioFileReader, EQ integration)
- Audio/Equalizer.cs — EqualizerSampleProvider (10-band peaking EQ)
- Audio/FrameEventArgs.cs — event args for metering frames
- Requirment.md — original requirements

Build & run
1. Restore packages: dotnet restore
2. Build: dotnet build
3. Run (from project dir): dotnet run --project Mp3Player.csproj

Or open the project in Visual Studio 2022/2026 and run.

Notes and limitations
- Echo, Reverb, and Stereo widening UI controls are present but effect implementations are placeholders. The audio pipeline is structured so these effects can be inserted between the EQ and output.
- Spectrum analyzer uses peak metering for a simple visualization. For frequency-band visualization use an FFT on sample buffers (FFTW or use NAudio's sample buffers + Math.NET Numerics for FFT).
- Seeking is not implemented on progress slider (hooking SliderProgress to audioFileReader.CurrentTime is required if you implement seeking).
- The equalizer uses fixed Q and peaking filters. You may want to tune Q values per band for musical behavior.

Next steps (suggested)
- Implement seeking (set audioFileReader.CurrentTime when user drags progress slider)
- Replace simple metering visualization with FFT frequency-band analyzer
- Implement Echo and Reverb effects using delay lines or convolution reverb
- Add presets and persistence (save EQ settings)
- Add unit or integration tests for audio chain components

If you want a developer guide with details on where to modify things, open DEVELOPMENT.md.
