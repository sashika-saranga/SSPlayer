Create a windows mp3 player with c# WPF that has following features.
Use NAudio nuget package.

User should be able to select a folder, then all the mp3 files in the selected folder(not sub folders) are listed in 'Track List'
There should be a Play/Pause button, Previous button, Next button, play progress bar, volume bar.
When a track is clicked from list it should start playing automatically. 
Below the player block there should be a Spectrum analyzer
Below the spectrum block there should be Audio settings block that has 10 band equalizer, Bass boost, treble boost, echo, reverb, sterio widening with below kind of blow

```
            MP3
             │
             ▼
      MP3 Decoder
      AudioFileReader
             │
             ▼
      EQ / Filters
             │
             ▼
          Echo
             │
             ▼
         Reverb
             │
             ▼
    Volume / Balance
             │
             ▼
      Limiter/Output
             │
             ▼
          WASAPI
             │
             ▼
         Speakers
```

There should be a proper simple and maintainable project structure as well. 

Fix-1 (updated)
Equalizer should be 15-band with vertical controls. Use the following common 15-band center frequencies (Hz):


| Band | Frequency |
| ---- | --------- |
| 1    | 25 Hz     |
| 2    | 40 Hz     |
| 3    | 63 Hz     |
| 4    | 100 Hz    |
| 5    | 160 Hz    |
| 6    | 250 Hz    |
| 7    | 400 Hz    |
| 8    | 630 Hz    |
| 9    | 1.00 kHz  |
| 10   | 1.60 kHz  |
| 11   | 2.50 kHz  |
| 12   | 4.00 kHz  |
| 13   | 6.30 kHz  |
| 14   | 10.00 kHz |
| 15   | 16.00 kHz |


Audio spectrum should have the same number of bands (15) and visually map to the EQ bands. The spectrum must resize to fit the player's width (bars sized and spaced to fill the SpectrumCanvas). The EQ control panel should be horizontally scrollable when necessary and sliders must be vertical.

Fix/Feature 2
Track list should be expand and shrink from left with a button (From main player.)
Main player should include Play controlls, Track info(new feature and explained below, EQ, spectrum)
When clicked on a track from track list, it should start playing(Mentioned before as well but not working)
Track info block should display below items
-Track name(running)
-Time elapsed from total time of track
-Volume level
-Playing/paused status
Track info block UI should be like Old HiFi monochrome LCD with green backlight.

Fix/Feature 3
Add advanced audio effects, seeking, and persistence features:

- Echo (delay) effect
  - UI: a checkbox to enable/disable echo and two sliders for Delay (ms, range 50..1000) and Feedback (0..0.95).
  - Behavior: insert a configurable delay line after the EQ in the audio chain. Feedback parameter controls the echo decay.
- Reverb effect
  - UI: a checkbox to enable/disable reverb and a slider for Decay (0..1) and Mix (dry/wet 0..1).
  - Behavior: implement a simple Schroeder-style reverb (comb + allpass network) or a lightweight convolution-based approximation; allow tuning Decay and Mix.
- Stereo Widening
  - UI: a checkbox and a slider for Width (0..1) where 0 = mono, 1 = maximum widening.
  - Behavior: apply mid/side processing to adjust stereo width between channels.
- Seeking
  - UI: make the progress Slider seekable. When user drags/releases the slider, set audioFileReader.CurrentTime accordingly.
  - Behavior: throttle updates while dragging to avoid jitter; update UI position when playback progresses.
- Presets & Persistence
  - UI: add a Presets dropdown with Save/Load/Delete actions.
  - Behavior: persist EQ gains, effect settings (echo/reverb/stereo), and volume to a JSON file in user AppData. Load on startup if present.
- Testing & Validation
  - Add manual test instructions for each effect (example audio files, parameter ranges, expected audible changes).
  - Ensure effects run off the UI thread and are safely disposed on Stop/OnClosed.

Notes

- Implement effects as modular ISampleProvider components inserted into AudioPlayer.PlayFile between the EqualizerSampleProvider and SampleAggregator/Metering provider. Keep latency bounded and document parameter ranges.
- If implementing convolution reverb requires extra dependencies, prefer a lightweight network reverb implemented in managed code to avoid heavy native payloads.

Fix/Feature 3

Add EQ reset button to EQ block.
Add new feature 'Manage Audio Files' as below.
-there should be a toggle on right side (just like trac list toggle) to expand this block to the right side
-On to there should be a radio button to enable this
-There should be a Track name text field, when track is clicked the name appeard on it. User can change it. It applies when another track is selected or click outside. Then track list should refresh with new name.
-There should be a delete button. When track is selected and delete is clicked it is marked to delete. When another track or outside clicked the original file will be deleted and refresh the terack list. 
-Above operation is risky, so the warning should be given when the enable checkbox is clicked. 

Fix 4
When open first time, right side toggle opens as empty. When shrink and expand only the content is shown.
When the track is clicked its not playing now. 
WHen the right pnel is expanded and enabled and track is clicked, its name is not loaded to rename field.

UI Fixes 5  
The right side 'Manage audio files' panel should increse its with by 1.5x.  
The main player area (Without left and right toggle blocks) should have the look of 90s HIFI player.  
Background should be dark gray.  
Buttons should be big rectangle and below buttons there should be a white text. On the button there should be a tiny ovel shape green button that lights up when click or dims up when mouseover.  
Slides should have big rectangle part to hold and slide. 

Fixes 6  
There should be 4 points in the edge of visible area of UI that allows user to resize the App. Those points are not visible. Check and fix them.  

Track list highlight should be light green and text should be black.  

Power button red light should be light up in red when the app is On and ready to use.  
When the player is not ready it should be not light up in red.   

From 'Manage Audio Files' block when a song is Marked to delete, it should highlight in red in Track list. And when Mark Delete is pressed again the marking should clear. If something is marked and some other track is clicked only the marked track should be deleted.   

Fixes 7  

-When the power button clicks, make sure all resources are released and completely exit the program.  
-Do improvements on UI(Buttons, Monochrome displays, text, sliders, player background) to make the player look like realistic 90s HiFi audio player.  
-Make sure any functionality is not harmed.