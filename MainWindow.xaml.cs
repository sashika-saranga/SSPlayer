using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows.Controls.Primitives;
using Mp3Player.Audio;
using System.Text.Json;

namespace Mp3Player
{
    public class RadioStation
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<double>? Frequencies { get; set; }
    }

    public partial class MainWindow : Window
    {
        private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                SetupTunerVisuals();
                // set initial indicator position
                tunerCurrentFreq = Math.Clamp(tunerCurrentFreq, TunerMinFreq, TunerMaxFreq);
                UpdateTunerIndicatorPosition();

                tunerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
                tunerTimer.Tick += TunerTimer_Tick;
            }
            catch { }
        }

        private void SetupTunerVisuals()
        {
            try
            {
                if (TunerCanvas == null) return;
                TunerCanvas.Children.Clear();
                double width = TunerCanvas.ActualWidth;
                double height = TunerCanvas.ActualHeight;
                if (width == 0 || height == 0)
                {
                    // set a default size if not measured yet
                    width = 104; height = 304;
                }

                // Draw subtle green backlight glow and central horizontal track (vintage tuner style)
                var ledColor = Color.FromRgb(0xA7, 0xF0, 0x7B);
                var ledBrush = new SolidColorBrush(ledColor);

                // glow behind the track
                var glow = new Rectangle { Height = 14, Fill = ledBrush, Opacity = 0.18, RadiusX = 8, RadiusY = 8 };
                Canvas.SetTop(glow, (height - glow.Height) / 2);
                Canvas.SetLeft(glow, 8);
                glow.Width = Math.Max(0, width - 16);
                try { glow.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = ledColor, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.9 }; } catch { }
                TunerCanvas.Children.Add(glow);

                var track = new Rectangle { Height = 4, Fill = new SolidColorBrush(Color.FromRgb(0x12,0x12,0x12)), RadiusX = 2, RadiusY = 2 };
                Canvas.SetTop(track, (height - track.Height) / 2);
                Canvas.SetLeft(track, 8);
                track.Width = width - 16;
                TunerCanvas.Children.Add(track);

                // Draw ticks and labels every 0.5 MHz across the width, label every 1.0 MHz
                double range = TunerMaxFreq - TunerMinFreq;
                int steps = (int)Math.Round(range / 0.5);
                for (int i = 0; i <= steps; i++)
                {
                    double freq = TunerMinFreq + i * 0.5;
                    double rel = (freq - TunerMinFreq) / range; // 0..1 left..right
                    double x = 8 + rel * (width - 16);

                    double tickHeight = (i % 2 == 0) ? 14 : 8; // major every 1.0 MHz
                    var tick = new Line { Y1 = (height / 2) - tickHeight / 2, Y2 = (height / 2) + tickHeight / 2, X1 = x, X2 = x, Stroke = ledBrush, StrokeThickness = 1 };
                    TunerCanvas.Children.Add(tick);

                    if (Math.Abs(freq * 10 % 10) < 0.001) // integer or .0
                    {
                        var lbl = new TextBlock { Text = freq.ToString("0"), Foreground = ledBrush, FontSize = 11 };
                        // place label slightly below the track
                        Canvas.SetLeft(lbl, x - 12);
                        Canvas.SetTop(lbl, (height / 2) + tickHeight / 2 + 4);
                        TunerCanvas.Children.Add(lbl);
                    }
                }

                // ensure indicator is on top of the drawn scale
                try
                {
                    if (TunerIndicator != null)
                    {
                        // size indicator to fit canvas height nicely
                        TunerIndicator.Height = Math.Min(64, Math.Max(24, height - 16));
                        if (!TunerCanvas.Children.Contains(TunerIndicator))
                            TunerCanvas.Children.Add(TunerIndicator);
                        // give the indicator a small glow for realism
                        try
                        {
                            TunerIndicator.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33));
                            TunerIndicator.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Red, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.85 };
                        }
                        catch { }
                        UpdateTunerIndicatorPosition();
                    }
                }
                catch { }
            }
            catch { }
        }

        private void TunerTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (Math.Abs(tunerTargetFreq - tunerCurrentFreq) < 0.001)
                {
                    // reached
                    tunerTimer.Stop();
                    tunerCurrentFreq = tunerTargetFreq;
                    UpdateTunerIndicatorPosition();
                    TxtRadioFrequency.Text = tunerCurrentFreq.ToString("0.0") + " MHz";
                    // start playback for pending url
                    if (!string.IsNullOrEmpty(tunerPendingUrl))
                    {
                        try
                        {
                            player.PlayStream(tunerPendingUrl, (float)SliderVolume.Value);
                            // update displayed station name if we can find a matching entry
                            try
                            {
                                var matchIdx = radioEntries.FindIndex(r => r.station != null && r.station.Url == tunerPendingUrl && Math.Abs(r.frequency - tunerCurrentFreq) < 0.01);
                                if (matchIdx >= 0)
                                {
                                    currentRadioEntryIndex = matchIdx;
                                    TxtRadioStationName.Text = radioEntries[matchIdx].station.Name;
                                }
                                else
                                {
                                    // try to match by URL only
                                    var byUrl = radioEntries.FindIndex(r => r.station != null && r.station.Url == tunerPendingUrl);
                                    if (byUrl >= 0)
                                    {
                                        currentRadioEntryIndex = byUrl;
                                        TxtRadioStationName.Text = radioEntries[byUrl].station.Name;
                                    }
                                }
                                TxtPlayStatus.Text = "Playing";
                            }
                            catch { }
                        }
                        catch { TxtPlayStatus.Text = "Radio play error"; }
                        tunerPendingUrl = null;
                    }
                    // update scan button enabled state when we arrive
                    try
                    {
                        if (BtnScanBack != null) BtnScanBack.IsEnabled = tunerCurrentFreq > TunerMinFreq + 0.0001;
                        if (BtnScanForward != null) BtnScanForward.IsEnabled = tunerCurrentFreq < TunerMaxFreq - 0.0001;
                    }
                    catch { }
                    return;
                }

                double dir = Math.Sign(tunerTargetFreq - tunerCurrentFreq);
                double step = TunerStepPerSecond * (tunerTimer.Interval.TotalSeconds);
                double diff = Math.Abs(tunerTargetFreq - tunerCurrentFreq);
                if (step > diff) step = diff;
                tunerCurrentFreq += dir * step;
                UpdateTunerIndicatorPosition();
                TxtRadioFrequency.Text = tunerCurrentFreq.ToString("0.0") + " MHz";
                // if we're sliding into band edges, update buttons as we go
                try
                {
                    if (BtnScanBack != null) BtnScanBack.IsEnabled = tunerCurrentFreq > TunerMinFreq + 0.0001;
                    if (BtnScanForward != null) BtnScanForward.IsEnabled = tunerCurrentFreq < TunerMaxFreq - 0.0001;
                }
                catch { }
            }
            catch { }
        }

        private void UpdateTunerIndicatorPosition()
        {
            try
            {
                if (TunerCanvas == null) return;
                double height = TunerCanvas.ActualHeight;
                double width = TunerCanvas.ActualWidth;
                if (height == 0) height = 80;
                if (width == 0) width = 300;
                double range = TunerMaxFreq - TunerMinFreq;
                double rel = (tunerCurrentFreq - TunerMinFreq) / range; // 0..1 left..right
                rel = Math.Clamp(rel, 0.0, 1.0);
                double x = 8 + rel * (width - 16); // same mapping as Setup
                // position indicator at x inside the canvas
                try
                {
                    Canvas.SetLeft(TunerIndicator, x - (TunerIndicator.Width / 2.0));
                    Canvas.SetTop(TunerIndicator, (height - TunerIndicator.Height) / 2.0);
                }
                catch { }
            }
            catch { }
        }


        private UserSettings userSettings = new UserSettings();
        private readonly DispatcherTimer settingsSaveTimer = new DispatcherTimer();
        private readonly AudioPlayer player = new AudioPlayer();
        private readonly List<string> tracks = new List<string>();
        private int currentIndex = -1;
        private readonly DispatcherTimer uiTimer;
        private bool isListVisible = true;
        private double leftColumnWidth = 280;
        private readonly DispatcherTimer marqueeTimer;
        private bool isSeeking = false;
        private bool allowClose = false;
        // manual end-detection guard
        private bool manualEndTriggered = false;
        private readonly double endDetectThresholdSeconds = 0.8; // when within this many seconds of end, treat as finished
        private string currentTrackFullName = string.Empty;
        private int marqueePos = 0;
        private readonly DispatcherTimer markedTimer;
        // Radio support
        private List<RadioStation> radioStations = new List<RadioStation>();
        private List<(RadioStation station, double frequency)> radioEntries = new List<(RadioStation, double)>();
        private int currentRadioEntryIndex = -1;
        // Tuner animation/visuals
        private DispatcherTimer tunerTimer;
        private double tunerCurrentFreq = 98.0;
        private double tunerTargetFreq = 98.0;
        private string? tunerPendingUrl = null;
        private const double TunerMinFreq = 87.5;
        private const double TunerMaxFreq = 108.0;
        private const double TunerStepPerSecond = 2.0; // MHz per second
        // effect enable flags are managed via AudioPlayer; no local persistent flags required
        // Manage-audio state
        private bool manageEnabled = false;
        private int? pendingDeleteIndex = null;
        private int pendingRenameIndex = -1;
        private string? pendingRenameName = null;
        private double rightPanelWidth = 480;
        // retained visuals for spectrum to avoid per-frame allocations
        private System.Windows.Shapes.Rectangle[][]? spectrumRects = null;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;

            player.SampleFramesAvailable += Player_SampleFramesAvailable;
            player.PlaybackEnded += Player_PlaybackEnded;

            // detect clicks outside manage panel to commit pending operations
            this.PreviewMouseDown += OnWindowPreviewMouseDown;

            uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();

            // marquee timer for track name scrolling
            marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            marqueeTimer.Tick += MarqueeTimer_Tick;

            // marked notice timer
            markedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            markedTimer.Tick += (s, e) =>
            {
                TxtMarkedNotice.Visibility = Visibility.Collapsed;
                markedTimer.Stop();
            };

            // load persisted settings
            userSettings = SettingsService.Load();

            // apply settings to UI and audio
            SliderVolume.Value = userSettings.Volume;
            SliderEcho.Value = userSettings.EchoLevel;
            SliderReverb.Value = userSettings.ReverbLevel;
            SliderStereo.Value = userSettings.StereoLevel;
            SliderBass.Value = userSettings.BassLevel;
            SliderTreble.Value = userSettings.TrebleLevel;
            // set EQ bands
            try
            {
                for (int i = 0; i < EqualizerSampleProvider.Frequencies.Length; i++)
                {
                    var ctrl = this.FindName($"Band{i}") as Slider;
                    if (ctrl != null) ctrl.Value = userSettings.EqGains.Length > i ? userSettings.EqGains[i] : 0f;
                }
            }
            catch { }

            // ensure tuner visuals update when control is resized
            try
            {
                if (TunerCanvas != null)
                {
                    TunerCanvas.SizeChanged += (s, e) =>
                    {
                        SetupTunerVisuals();
                        UpdateTunerIndicatorPosition();
                    };
                }
            }
            catch { }

            // ensure play indicator initial state
            SetButtonIndicator(BtnPlayPause, false);
            UpdatePowerIndicator();

            // initialize audio effect levels from persisted settings
            player.UpdateEchoLevel(userSettings.EchoLevel);
            player.UpdateReverbLevel(userSettings.ReverbLevel);
            player.UpdateStereoLevel(userSettings.StereoLevel);
            player.UpdateBassLevel(userSettings.BassLevel);
            player.UpdateTrebleLevel(userSettings.TrebleLevel);
            // enable effects state
            IndicatorHelper.SetIsIndicatorOn(BtnEcho, userSettings.EchoEnabled);
            SliderEcho.IsEnabled = userSettings.EchoEnabled;
            player.EnableEcho(userSettings.EchoEnabled);
            IndicatorHelper.SetIsIndicatorOn(BtnReverb, userSettings.ReverbEnabled);
            SliderReverb.IsEnabled = userSettings.ReverbEnabled;
            player.EnableReverb(userSettings.ReverbEnabled);
            IndicatorHelper.SetIsIndicatorOn(BtnStereo, userSettings.StereoEnabled);
            SliderStereo.IsEnabled = userSettings.StereoEnabled;
            player.EnableStereo(userSettings.StereoEnabled);
            // bass/treble UI state (use buttons as effect toggles)
            IndicatorHelper.SetIsIndicatorOn(BtnBass, userSettings.BassEnabled);
            SliderBass.IsEnabled = userSettings.BassEnabled;
            player.EnableBass(userSettings.BassEnabled);
            IndicatorHelper.SetIsIndicatorOn(BtnTreble, userSettings.TrebleEnabled);
            SliderTreble.IsEnabled = userSettings.TrebleEnabled;
            player.EnableTreble(userSettings.TrebleEnabled);

            // clipping indicator subscription
            player.ClippingChanged += Player_ClippingChanged;

            // load radio stations (non-fatal)
            try { LoadRadioStations(); } catch { }

            // prepare tuner visuals when window is loaded
            this.Loaded += MainWindow_Loaded;

            // schedule settings save timer (debounce)
            settingsSaveTimer.Interval = TimeSpan.FromSeconds(5);
            settingsSaveTimer.Tick += (s, e) =>
            {
                settingsSaveTimer.Stop();
                SettingsService.Save(userSettings);
            };
        }

        private void LoadRadioStations()
        {
            string dataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "radio_stations.json");
            if (!File.Exists(dataPath))
            {
                // try relative path for dev-time
                dataPath = System.IO.Path.Combine("Data", "radio_stations.json");
                if (!File.Exists(dataPath)) return;
            }

            var json = File.ReadAllText(dataPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            radioStations = JsonSerializer.Deserialize<List<RadioStation>>(json, options) ?? new List<RadioStation>();

            // build flattened entries list
            radioEntries.Clear();
            foreach (var s in radioStations)
            {
                if (s.Frequencies != null && s.Frequencies.Count > 0)
                {
                    foreach (var f in s.Frequencies)
                        radioEntries.Add((s, f));
                }
                else
                {
                    // if no frequency, still add with 0
                    radioEntries.Add((s, 0));
                }
            }

            // no stored list UI (tuner shows full FM band)
        }

        private void PlayRadioEntryIndex(int index)
        {
            if (index < 0 || index >= radioEntries.Count) return;
            currentRadioEntryIndex = index;
            var entry = radioEntries[index];
            TxtRadioStationName.Text = entry.station.Name;
            if (entry.frequency > 0)
            {
                // start smooth tuner move to the target frequency, then play stream when reached
                tunerTargetFreq = entry.frequency;
                tunerPendingUrl = entry.station.Url;
                if (tunerTimer != null && !tunerTimer.IsEnabled)
                    tunerTimer.Start();
            }
            else
            {
                TxtRadioFrequency.Text = "-- MHz";
                // no frequency to animate to, play immediately
                try { player.PlayStream(entry.station.Url, (float)SliderVolume.Value); }
                catch { TxtPlayStatus.Text = "Radio play error"; }
            }
        }

        private void BtnScanForward_Click(object sender, RoutedEventArgs e)
        {
            // Start scanning forward only. Find next stored station frequency above current tuner freq.
            if (tunerCurrentFreq >= TunerMaxFreq - 0.0001)
            {
                BtnScanForward.IsEnabled = false;
                return;
            }
            // If a station is currently playing, stop it immediately as soon as scanning begins
            try
            {
                if (player != null && player.IsPlaying)
                {
                    player.Stop();
                    TxtPlayStatus.Text = "Scanning...";
                    // clear current radio entry index while scanning away
                    currentRadioEntryIndex = -1;
                }
            }
            catch { }

            // find the next station frequency greater than current frequency
            var candidate = radioEntries.Where(r => r.frequency > tunerCurrentFreq).OrderBy(r => r.frequency).FirstOrDefault();
            if (candidate.station != null && candidate.frequency > 0)
            {
                tunerTargetFreq = Math.Min(candidate.frequency, TunerMaxFreq);
                tunerPendingUrl = candidate.station.Url;
                // update currentRadioEntryIndex if we can find it
                int idx = radioEntries.FindIndex(r => r.station == candidate.station && Math.Abs(r.frequency - candidate.frequency) < 0.0001);
                if (idx >= 0) currentRadioEntryIndex = idx;
            }
            else
            {
                // no next station - move to right end and disable forward
                tunerTargetFreq = TunerMaxFreq;
                tunerPendingUrl = null;
            }

            BtnScanBack.IsEnabled = true;
            if (tunerTimer != null && !tunerTimer.IsEnabled) tunerTimer.Start();
        }

        private void BtnScanBack_Click(object sender, RoutedEventArgs e)
        {
            // Start scanning backward only. Find previous stored station frequency below current tuner freq.
            if (tunerCurrentFreq <= TunerMinFreq + 0.0001)
            {
                BtnScanBack.IsEnabled = false;
                return;
            }
            // If a station is currently playing, stop it immediately as soon as scanning begins
            try
            {
                if (player != null && player.IsPlaying)
                {
                    player.Stop();
                    TxtPlayStatus.Text = "Scanning...";
                    currentRadioEntryIndex = -1;
                }
            }
            catch { }

            var candidate = radioEntries.Where(r => r.frequency < tunerCurrentFreq && r.frequency > 0).OrderByDescending(r => r.frequency).FirstOrDefault();
            if (candidate.station != null && candidate.frequency > 0)
            {
                tunerTargetFreq = Math.Max(candidate.frequency, TunerMinFreq);
                tunerPendingUrl = candidate.station.Url;
                int idx = radioEntries.FindIndex(r => r.station == candidate.station && Math.Abs(r.frequency - candidate.frequency) < 0.0001);
                if (idx >= 0) currentRadioEntryIndex = idx;
            }
            else
            {
                // no previous station - move to left end and disable back
                tunerTargetFreq = TunerMinFreq;
                tunerPendingUrl = null;
            }

            BtnScanForward.IsEnabled = true;
            if (tunerTimer != null && !tunerTimer.IsEnabled) tunerTimer.Start();
        }


        private void UpdatePowerIndicator()
        {
            // Power light is on when the app is ready (folder loaded with tracks)
            SetButtonIndicator(BtnPower, tracks.Count > 0);
        }

        private void RefreshTrackListHighlights()
        {
            try
            {
                for (int i = 0; i < ListTracks.Items.Count; i++)
                {
                    if (ListTracks.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item)
                        continue;

                    if (pendingDeleteIndex == i)
                    {
                        item.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));
                        item.Foreground = Brushes.Black;
                    }
                    else if (ListTracks.SelectedIndex == i)
                    {
                        item.Background = new SolidColorBrush(Color.FromRgb(0xA7, 0xF0, 0x7B));
                        item.Foreground = Brushes.Black;
                    }
                    else
                    {
                        item.ClearValue(ListBoxItem.BackgroundProperty);
                        item.ClearValue(ListBoxItem.ForegroundProperty);
                    }
                }
            }
            catch { }
        }

        private void Player_ClippingChanged(object? sender, bool isClipping)
        {
            try
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var el = this.FindName("ClipIndicator") as System.Windows.Shapes.Ellipse;
                        if (el == null) return;
                        if (isClipping)
                        {
                            try
                            {
                                var onBrush = this.FindResource("LedOnBrushRed") as System.Windows.Media.Brush;
                                var glow = this.FindResource("LedGlowEffectRed") as System.Windows.Media.Effects.Effect;
                                if (onBrush != null) el.Fill = onBrush;
                                el.Effect = glow;
                                el.Opacity = 1.0; // fully visible when lit
                            }
                            catch { /* fallback handled below */ }
                        }
                        else
                        {
                            try
                            {
                                var offBrush = this.FindResource("LedOffBrushRed") as System.Windows.Media.Brush;
                                if (offBrush != null) el.Fill = offBrush;
                                el.Effect = null;
                                el.Opacity = 0.18; // dim but present to avoid layout shift
                            }
                            catch { /* ignore */ }
                        }
                    }
                    catch { }
                }));
            }
            catch { }
        }

        private void ScheduleTrackListHighlightRefresh()
        {
            Dispatcher.BeginInvoke(new Action(RefreshTrackListHighlights), DispatcherPriority.Loaded);
        }

        private void ScheduleSettingsSave()
        {
            try
            {
                if (settingsSaveTimer.IsEnabled)
                {
                    settingsSaveTimer.Stop();
                }
                settingsSaveTimer.Start();
            }
            catch { }
        }

        private void SliderEcho_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                player.UpdateEchoLevel((float)e.NewValue);
                userSettings.EchoLevel = (float)e.NewValue;
                ScheduleSettingsSave();
            }
            catch { }
        }

        private void SliderReverb_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                player.UpdateReverbLevel((float)e.NewValue);
                userSettings.ReverbLevel = (float)e.NewValue;
                ScheduleSettingsSave();
            }
            catch { }
        }

        private void SliderStereo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                player.UpdateStereoLevel((float)e.NewValue);
                userSettings.StereoLevel = (float)e.NewValue;
                ScheduleSettingsSave();
            }
            catch { }
        }

        private void SliderBass_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                player.UpdateBassLevel((float)e.NewValue);
                userSettings.BassLevel = (float)e.NewValue;
                ScheduleSettingsSave();
            }
            catch { }
        }

        private void SliderTreble_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                player.UpdateTrebleLevel((float)e.NewValue);
                userSettings.TrebleLevel = (float)e.NewValue;
                ScheduleSettingsSave();
            }
            catch { }
        }


        private void Player_PlaybackEnded(object? sender, EventArgs e)
        {
            // advance to next track on UI thread
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (tracks.Count == 0) return;
                    int next = (currentIndex + 1) % tracks.Count;
                    PlayIndex(next);
                }
                catch { }
            });
        }

        private void MainPlayerBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // allow dragging the window by holding anywhere on main player
                this.DragMove();
            }
            catch { }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // show drop shadow while dragging
                var shadow = new System.Windows.Media.Effects.DropShadowEffect { Color = System.Windows.Media.Colors.Black, BlurRadius = 12, Opacity = 0.6, Direction = 270, ShadowDepth = 6 };
                MainPlayerBorder.Effect = shadow;
                this.DragMove();
                MainPlayerBorder.Effect = null;
            }
            catch { }
        }

        private const double MinWinWidth = 400;
        private const double MinWinHeight = 200;

        private void ThumbTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            try
            {
                double newWidth = this.Width - e.HorizontalChange;
                double newHeight = this.Height - e.VerticalChange;
                double newLeft = this.Left + e.HorizontalChange;
                double newTop = this.Top + e.VerticalChange;
                if (newWidth >= MinWinWidth)
                {
                    this.Width = newWidth;
                    this.Left = newLeft;
                }
                if (newHeight >= MinWinHeight)
                {
                    this.Height = newHeight;
                    this.Top = newTop;
                }
            }
            catch { }
        }

        private void ThumbTopRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            try
            {
                double newWidth = this.Width + e.HorizontalChange;
                double newHeight = this.Height - e.VerticalChange;
                double newTop = this.Top + e.VerticalChange;
                if (newWidth >= MinWinWidth) this.Width = newWidth;
                if (newHeight >= MinWinHeight)
                {
                    this.Height = newHeight;
                    this.Top = newTop;
                }
            }
            catch { }
        }

        private void ThumbBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            try
            {
                double newWidth = this.Width - e.HorizontalChange;
                double newHeight = this.Height + e.VerticalChange;
                double newLeft = this.Left + e.HorizontalChange;
                if (newWidth >= MinWinWidth)
                {
                    this.Width = newWidth;
                    this.Left = newLeft;
                }
                if (newHeight >= MinWinHeight) this.Height = newHeight;
            }
            catch { }
        }

        private void ThumbBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            try
            {
                double newWidth = this.Width + e.HorizontalChange;
                double newHeight = this.Height + e.VerticalChange;
                if (newWidth >= MinWinWidth) this.Width = newWidth;
                if (newHeight >= MinWinHeight) this.Height = newHeight;
            }
            catch { }
        }

        private void SetButtonIndicator(System.Windows.Controls.Button btn, bool on)
        {
            try
            {
                if (btn == null) return;
                // set attached DP so template triggers can react
                IndicatorHelper.SetIsIndicatorOn(btn, on);
            }
            catch { }
        }

        private void RbEnableManage_Checked(object sender, RoutedEventArgs e)
        {
            var res = System.Windows.MessageBox.Show("Enabling manage mode allows permanent file operations (rename/delete). Do you want to enable?", "Enable Manage Mode", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes)
            {
                RbEnableManage.IsChecked = false;
                manageEnabled = false;
                TxtManageWarning.Visibility = Visibility.Collapsed;
                return;
            }
            manageEnabled = true;
            TxtManageWarning.Visibility = Visibility.Visible;
            // show and enable controls
            if (BtnDelete != null) { BtnDelete.Visibility = Visibility.Visible; BtnDelete.IsEnabled = true; }
            if (BtnRename != null) { BtnRename.Visibility = Visibility.Visible; BtnRename.IsEnabled = true; }
            if (TxtRename != null) TxtRename.IsEnabled = true;
            UpdateManageSelection();
        }

        private void RbEnableManage_Unchecked(object sender, RoutedEventArgs e)
        {
            manageEnabled = false;
            TxtManageWarning.Visibility = Visibility.Collapsed;
            // clear any pending ops
            pendingDeleteIndex = null;
            pendingRenameIndex = -1;
            pendingRenameName = null;
            ScheduleTrackListHighlightRefresh();
            // hide/disable controls
            if (BtnDelete != null) { BtnDelete.IsEnabled = false; }
            if (BtnRename != null) { BtnRename.IsEnabled = false; }
            if (TxtRename != null) TxtRename.IsEnabled = false;
            UpdateManageSelection();
        }

        // Removed automatic rename on LostFocus. Rename now applies only when Apply button is clicked.

        private void OnWindowPreviewMouseDown(object? sender, MouseButtonEventArgs e)
        {
            // if user clicks outside the ManageAudioGroup, commit pending operations
            try
            {
                var src = e.OriginalSource as DependencyObject;
                bool inside = false;
                while (src != null)
                {
                    if (src == ManageAudioGroup) { inside = true; break; }
                    src = System.Windows.Media.VisualTreeHelper.GetParent(src);
                }
                if (!inside)
                {
                    // Delay committing so selection change on listbox can occur first
                    this.Dispatcher.BeginInvoke(new Action(() => CommitPendingOperations()), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch { }
        }

        private void BtnPower_Click(object sender, RoutedEventArgs e)
        {
            // Power button: permit close and close window
            try
            {
                allowClose = true;
                this.Close();
            }
            catch { }
        }

        private bool isMaximized = false;
        private double prevLeft, prevTop, prevWidth, prevHeight;

        private void BtnMaxRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!isMaximized)
                {
                    // Use WindowState for maximizing to let WPF arrange layout correctly
                    prevLeft = this.Left; prevTop = this.Top; prevWidth = this.Width; prevHeight = this.Height;
                    this.WindowState = WindowState.Maximized;
                    isMaximized = true;
                }
                else
                {
                    this.WindowState = WindowState.Normal;
                    // restore previous position/size
                    this.Left = prevLeft; this.Top = prevTop; this.Width = prevWidth; this.Height = prevHeight;
                    isMaximized = false;
                }
            }
            catch { }
        }

        private void CommitPendingOperations()
        {
            if (!pendingDeleteIndex.HasValue) return;
            int deleteIndex = pendingDeleteIndex.Value;
            pendingDeleteIndex = null;
            CommitPendingDelete(deleteIndex);
        }

        private void CommitPendingDelete(int deleteIndex, Action? onComplete = null)
        {
            if (deleteIndex < 0) return;

            Task.Run(() =>
            {
                try
                {
                    int idx = deleteIndex;
                    string? path = null;
                    this.Dispatcher.Invoke(() => { if (idx >= 0 && idx < tracks.Count) path = tracks[idx]; });

                    if (!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            bool wasPlayingThis = false;
                            this.Dispatcher.Invoke(() => { wasPlayingThis = (currentIndex == idx && player.IsPlaying); });
                            if (wasPlayingThis) this.Dispatcher.Invoke(() => player.Stop());

                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                            this.Dispatcher.Invoke(() =>
                            {
                                if (idx >= 0 && idx < tracks.Count) tracks.RemoveAt(idx);
                                ListTracks.ItemsSource = null;
                                ListTracks.ItemsSource = tracks.Select(f => System.IO.Path.GetFileName(f));
                                if (tracks.Count == 0)
                                {
                                    currentIndex = -1;
                                    ListTracks.SelectedIndex = -1;
                                }
                                else if (currentIndex == idx)
                                {
                                    currentIndex = Math.Min(idx, tracks.Count - 1);
                                    ListTracks.SelectedIndex = currentIndex;
                                    PlayIndex(currentIndex);
                                }
                                else if (currentIndex > idx)
                                {
                                    currentIndex--;
                                }
                                UpdateManageSelection();
                                UpdatePowerIndicator();
                                ScheduleTrackListHighlightRefresh();
                                onComplete?.Invoke();
                            });
                        }
                        catch (Exception ex)
                        {
                            this.Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"Delete failed: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error));
                        }
                    }
                    else
                    {
                        this.Dispatcher.Invoke(() => onComplete?.Invoke());
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"Operation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void ShowMarkedNotice(string message)
        {
            try
            {
                TxtMarkedNotice.Text = message;
                TxtMarkedNotice.Visibility = Visibility.Visible;
                markedTimer.Stop();
                markedTimer.Start();
            }
            catch { }
        }

        private void Player_SampleFramesAvailable(object? sender, FrameEventArgs e)
        {
            // simple visualization: update retained bars from peak values
            // Use BeginInvoke so audio thread is not blocked by UI work
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Volumes == null) return;
                AnimateNeedlesFromVolumes(e.Volumes);
                int bands = EqualizerSampleProvider.Frequencies.Length; // match EQ band count
                const int bulbsPerBand = 9; // 4 green, 3 amber, 2 red
                const int greenCount = 4;
                const int amberCount = 3;
                // layout calculations
                double spacing = 6.0; // gap between bands
                double totalSpacing = spacing * (bands - 1);
                double bandWidth = bands > 0 ? Math.Max(20, (SpectrumCanvas.ActualWidth - totalSpacing) / bands) : 20;
                double bulbSpacing = 4.0; // gap between bulbs vertically
                double totalBulbSpacing = bulbSpacing * (bulbsPerBand - 1);
                double bulbHeight = Math.Max(4, (SpectrumCanvas.ActualHeight - totalBulbSpacing) / bulbsPerBand);
                double bulbWidth = Math.Max(8, bandWidth * 0.7);
                double leftOffset = (bandWidth - bulbWidth) / 2.0;

                // create retained rectangles if not created or band count changed
                if (spectrumRects == null || spectrumRects.Length != bands)
                {
                    SpectrumCanvas.Children.Clear();
                    spectrumRects = new System.Windows.Shapes.Rectangle[bands][];
                    for (int i = 0; i < bands; i++)
                    {
                        spectrumRects[i] = new System.Windows.Shapes.Rectangle[bulbsPerBand];
                        for (int b = 0; b < bulbsPerBand; b++)
                        {
                            var rect = new System.Windows.Shapes.Rectangle
                            {
                                Width = bulbWidth,
                                Height = bulbHeight,
                                Fill = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                                RadiusX = 2,
                                RadiusY = 2,
                                Stroke = Brushes.Black,
                                StrokeThickness = 1
                            };
                            spectrumRects[i][b] = rect;
                            SpectrumCanvas.Children.Add(rect);
                        }
                    }
                }

                // update positions and fills
                for (int i = 0; i < bands; i++)
                {
                    double level = 0;
                    if (i < e.Volumes.Length) level = e.Volumes[i];
                    level = Math.Max(0.0, Math.Min(1.0, level));

                    int lit = (int)Math.Round(level * bulbsPerBand);
                    if (lit < 0) lit = 0; if (lit > bulbsPerBand) lit = bulbsPerBand;

                    for (int b = 0; b < bulbsPerBand; b++)
                    {
                        double x = i * (bandWidth + spacing) + leftOffset;
                        double y = SpectrumCanvas.ActualHeight - ((b + 1) * bulbHeight + b * bulbSpacing);
                        var rect = spectrumRects[i][b];
                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, y);
                        rect.Width = bulbWidth;
                        rect.Height = bulbHeight;

                        bool on = b < lit;
                        if (!on) rect.Fill = new SolidColorBrush(Color.FromRgb(40, 40, 40));
                        else if (b < greenCount) rect.Fill = Brushes.LimeGreen;
                        else if (b < greenCount + amberCount) rect.Fill = Brushes.Orange;
                        else rect.Fill = Brushes.Red;
                    }
                }
            }));
        }

        private void AnimateNeedlesFromVolumes(float[] volumes)
        {
            try
            {
                double leftLevel = volumes.Length > 0 ? Math.Max(0.0, Math.Min(1.0, volumes[0])) : 0.0;
                double rightLevel = volumes.Length > 1 ? Math.Max(0.0, Math.Min(1.0, volumes[1])) : leftLevel;
                double minAngle = -45.0;
                double maxAngle = 45.0;
                double leftAngle = minAngle + (maxAngle - minAngle) * leftLevel;
                double rightAngle = minAngle + (maxAngle - minAngle) * rightLevel;

                // prefer named RotateTransform on the parent Canvas
                var rtL = this.FindName("NeedleLeftRotate") as RotateTransform;
                var rtR = this.FindName("NeedleRightRotate") as RotateTransform;

                // fallback: try to locate any element and use its RenderTransform
                if (rtL == null)
                {
                    var el = this.FindName("NeedleLeft") as FrameworkElement;
                    if (el?.RenderTransform is RotateTransform r) rtL = r;
                }
                if (rtR == null)
                {
                    var el = this.FindName("NeedleRight") as FrameworkElement;
                    if (el?.RenderTransform is RotateTransform r) rtR = r;
                }

                if (rtL == null && rtR == null) return;

                var daL = new System.Windows.Media.Animation.DoubleAnimation(leftAngle, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                var daR = new System.Windows.Media.Animation.DoubleAnimation(rightAngle, TimeSpan.FromMilliseconds(120))
                {
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                try { rtL?.BeginAnimation(RotateTransform.AngleProperty, daL); } catch { }
                try { rtR?.BeginAnimation(RotateTransform.AngleProperty, daR); } catch { }
                // static VU scale is drawn in XAML now; no dynamic label updates required
            }
            catch { }
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (player.TotalTime.TotalSeconds > 0)
                {
                    if (!isSeeking)
                    {
                        SliderProgress.Value = player.CurrentTime.TotalSeconds / player.TotalTime.TotalSeconds;
                    }
                    TxtTimeInfo.Text = $"{player.CurrentTime:mm\\:ss} / {player.TotalTime:mm\\:ss}";
                    TxtPlayStatus.Text = player.IsPlaying ? "Playing" : "Paused";
                    TxtVolumeLevel.Text = $"{(int)(SliderVolume.Value * 100)}%";
                }
                // additional end-of-track detection based on progress time
                try
                {
                    if (!isSeeking && player.TotalTime.TotalSeconds > 0 && player.IsPlaying)
                    {
                        var remaining = player.TotalTime - player.CurrentTime;
                        if (!manualEndTriggered && remaining.TotalSeconds >= 0 && remaining.TotalSeconds <= endDetectThresholdSeconds)
                        {
                            // guard to avoid duplicate triggers
                            manualEndTriggered = true;
                            // advance to next track on UI thread (same behavior as PlaybackEnded)
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (tracks.Count == 0) return;
                                    int next = (currentIndex + 1) % tracks.Count;
                                    PlayIndex(next);
                                }
                                catch { }
                            }));
                        }
                        else if (manualEndTriggered && remaining.TotalSeconds > endDetectThresholdSeconds)
                        {
                            // user moved away from end or playback jumped back; clear guard
                            manualEndTriggered = false;
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TxtFolder.Text = dlg.SelectedPath;
                LoadTracks(dlg.SelectedPath);
            }
        }

        private void LoadTracks(string folder)
        {
            tracks.Clear();
        IEnumerable<string> filesEnumerable;
        try
        {
            // include mp3 files in folder and all subfolders
            filesEnumerable = Directory.EnumerateFiles(folder, "*.mp3", SearchOption.AllDirectories);
        }
        catch (Exception)
        {
            // if access to some subfolders is denied, fall back to top-level only
            filesEnumerable = Directory.EnumerateFiles(folder, "*.mp3", SearchOption.TopDirectoryOnly);
        }
        var files = filesEnumerable.OrderBy(f => f).ToArray();
        tracks.AddRange(files);
            ListTracks.ItemsSource = tracks.Select(f => System.IO.Path.GetFileName(f));
            currentIndex = -1;
            UpdatePowerIndicator();
            ScheduleTrackListHighlightRefresh();
        }

        private void ListTracks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListTracks.SelectedIndex >= 0)
            {
                PlayIndex(ListTracks.SelectedIndex);
            }
        }

        private void SliderProgress_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isSeeking = true;
        }

        private void SliderProgress_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (!isSeeking) return;
                isSeeking = false;
                if (player.TotalTime.TotalSeconds > 0)
                {
                    double target = SliderProgress.Value * player.TotalTime.TotalSeconds;
                    player.Seek(TimeSpan.FromSeconds(target));
                    // clear manual end guard after explicit user seek
                    manualEndTriggered = false;
                }
            }
            catch { isSeeking = false; }
        }

        private void PlayIndex(int idx)
        {
            if (idx < 0 || idx >= tracks.Count) return;
            currentIndex = idx;
            // reset manual end guard when starting a new track
            manualEndTriggered = false;
            string file = tracks[idx];
            player.PlayFile(file, (float)SliderVolume.Value);
            BtnPlayPause.Content = "Pause";
            SetButtonIndicator(BtnPlayPause, true);
            // highlight currently playing track
            try
            {
                ListTracks.SelectedIndex = currentIndex;
                ListTracks.ScrollIntoView(ListTracks.SelectedItem);
            }
            catch { }
            ScheduleTrackListHighlightRefresh();
            // update track info
            currentTrackFullName = System.IO.Path.GetFileName(file) ?? string.Empty;
            TxtTrackName.Text = currentTrackFullName;
            marqueePos = 0;
            if (currentTrackFullName.Length > 30) marqueeTimer.Start(); else marqueeTimer.Stop();
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (player.IsPlaying)
            {
                player.Pause();
                BtnPlayPause.Content = "Play";
                SetButtonIndicator(BtnPlayPause, false);
            }
            else
            {
                if (currentIndex >= 0)
                {
                    player.Resume();
                    BtnPlayPause.Content = "Pause";
                    SetButtonIndicator(BtnPlayPause, true);
                }
                else if (tracks.Count > 0)
                {
                    PlayIndex(0);
                }
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (tracks.Count == 0) return;
            int next = (currentIndex + 1) % tracks.Count;
            PlayIndex(next);
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (tracks.Count == 0) return;
            int prev = (currentIndex - 1 + tracks.Count) % tracks.Count;
            PlayIndex(prev);
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            player.SetVolume((float)SliderVolume.Value);
            if(TxtVolumeLevel != null)
                TxtVolumeLevel.Text = $"{(int)(SliderVolume.Value * 100)}%";
            try
            {
                userSettings.Volume = SliderVolume.Value;
                ScheduleSettingsSave();
            }
            catch { }
        }

        private void BtnToggleList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var col = RootGrid.ColumnDefinitions[0];
                if (col.Width.Value > 0)
                {
                    leftColumnWidth = col.Width.Value;
                    col.Width = new GridLength(0);
                    isListVisible = false;
                }
                else
                {
                    col.Width = new GridLength(leftColumnWidth);
                    isListVisible = true;
                }
            }
            catch { }
        }

        private void ListTracks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int sel = ListTracks.SelectedIndex;

            // If another track is marked for delete, delete only the marked track
            if (pendingDeleteIndex.HasValue && pendingDeleteIndex.Value != sel)
            {
                int deleteIdx = pendingDeleteIndex.Value;
                int playIdx = sel;
                pendingDeleteIndex = null;
                CommitPendingDelete(deleteIdx, () =>
                {
                    int adjustedPlay = playIdx;
                    if (deleteIdx < playIdx) adjustedPlay--;
                    if (adjustedPlay >= 0 && adjustedPlay < tracks.Count)
                    {
                        ListTracks.SelectedIndex = adjustedPlay;
                        if (adjustedPlay != currentIndex)
                            PlayIndex(adjustedPlay);
                    }
                    ScheduleTrackListHighlightRefresh();
                });
                UpdateManageSelection();
                return;
            }

            if (sel >= 0 && sel < tracks.Count)
            {
                if (sel != currentIndex)
                {
                    PlayIndex(sel);
                }
                else
                {
                    ListTracks.SelectedIndex = sel;
                }
            }
            UpdateManageSelection();
            ScheduleTrackListHighlightRefresh();
        }

        private void BtnToggleAudio_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Toggle the Manage Audio Files panel and adjust right column width
                var col = RootGrid.ColumnDefinitions[2];
                if (col.Width.Value > 0)
                {
                    rightPanelWidth = col.Width.Value;
                    col.Width = new GridLength(0);
                    ManageAudioGroup.Visibility = Visibility.Collapsed;
                }
                else
                {
                    col.Width = new GridLength(rightPanelWidth);
                    ManageAudioGroup.Visibility = Visibility.Visible;
                    UpdateManageSelection();
                }
            }
            catch { }
        }

        private void UpdateManageSelection()
        {
            try
            {
                if (ListTracks.SelectedIndex >= 0 && ListTracks.SelectedIndex < tracks.Count)
                {
                    var path = tracks[ListTracks.SelectedIndex];
                    TxtSelectedFile.Text = System.IO.Path.GetFileName(path);
                    if (pendingDeleteIndex == ListTracks.SelectedIndex)
                    {
                        TxtSelectedFile.Text += " (marked for delete)";
                        TxtSelectedFile.Foreground = Brushes.Red;
                    }
                    else if (pendingRenameIndex == ListTracks.SelectedIndex && !string.IsNullOrEmpty(pendingRenameName))
                    {
                        TxtRename.Text = pendingRenameName;
                        TxtSelectedFile.Foreground = Brushes.Orange;
                    }
                    else
                    {
                        TxtRename.Text = System.IO.Path.GetFileName(path);
                        TxtSelectedFile.Foreground = Brushes.Black;
                    }
                    BtnRename.IsEnabled = manageEnabled;
                    BtnDelete.IsEnabled = manageEnabled;
                }
                else
                {
                    TxtSelectedFile.Text = "(none)";
                    TxtRename.Text = string.Empty;
                    BtnRename.IsEnabled = false;
                    BtnDelete.IsEnabled = false;
                }
            }
            catch { }
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (!manageEnabled)
            {
                System.Windows.MessageBox.Show("Enable Manage Mode to modify files.", "Manage Disabled", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ListTracks.SelectedIndex < 0) return;
            int idx = ListTracks.SelectedIndex;
            string oldPath = tracks[idx];
            string newName = TxtRename.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(newName))
            {
                System.Windows.MessageBox.Show("Enter a new file name.", "Rename", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // perform rename on background thread
            Task.Run(() =>
            {
                try
                {
                    string dir = System.IO.Path.GetDirectoryName(oldPath) ?? string.Empty;
                    string oldExt = System.IO.Path.GetExtension(oldPath);
                    string newExt = System.IO.Path.GetExtension(newName);
                    if (string.IsNullOrEmpty(newExt)) newName += oldExt;
                    string newPath = System.IO.Path.Combine(dir, newName);

                    if (System.IO.File.Exists(newPath))
                    {
                        this.Dispatcher.Invoke(() => System.Windows.MessageBox.Show("A file with that name already exists.", "Rename Failed", MessageBoxButton.OK, MessageBoxImage.Error));
                        return;
                    }

                    System.IO.File.Move(oldPath, newPath);
                    this.Dispatcher.Invoke(() =>
                    {
                        tracks[idx] = newPath;
                        ListTracks.ItemsSource = null;
                        ListTracks.ItemsSource = tracks.Select(f => System.IO.Path.GetFileName(f));
                        ListTracks.SelectedIndex = idx;
                        if (currentIndex == idx)
                        {
                            currentTrackFullName = System.IO.Path.GetFileName(newPath);
                            TxtTrackName.Text = currentTrackFullName;
                        }
                        UpdateManageSelection();
                        System.Windows.MessageBox.Show($"Renamed to {newName}", "Rename", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() => System.Windows.MessageBox.Show($"Rename failed: {ex.Message}", "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            });
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!manageEnabled)
            {
                System.Windows.MessageBox.Show("Enable Manage Mode to modify files.", "Manage Disabled", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ListTracks.SelectedIndex < 0) return;
            int idx = ListTracks.SelectedIndex;

            if (pendingDeleteIndex == idx)
            {
                pendingDeleteIndex = null;
                TxtSelectedFile.Foreground = Brushes.Black;
                TxtSelectedFile.Text = System.IO.Path.GetFileName(tracks[idx]);
                ShowMarkedNotice("Delete marking cleared");
            }
            else
            {
                pendingDeleteIndex = idx;
                TxtSelectedFile.Foreground = Brushes.Red;
                TxtSelectedFile.Text = System.IO.Path.GetFileName(tracks[idx]) + " (marked for delete)";
                ShowMarkedNotice("Marked for deletion");
            }

            UpdateManageSelection();
            ScheduleTrackListHighlightRefresh();
        }

        private void MarqueeTimer_Tick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentTrackFullName)) return;
            int displayLen = 60;
            if (currentTrackFullName.Length <= displayLen)
            {
                TxtTrackName.Text = currentTrackFullName;
                marqueeTimer.Stop();
                return;
            }
            if (marqueePos >= currentTrackFullName.Length) marqueePos = 0;
            string s = currentTrackFullName + "   ";
            if (marqueePos + displayLen <= s.Length)
                TxtTrackName.Text = s.Substring(marqueePos, displayLen);
            else
            {
                int first = s.Length - marqueePos;
                TxtTrackName.Text = s.Substring(marqueePos, first) + s.Substring(0, displayLen - first);
            }
            marqueePos++;
        }

        private void SliderProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // seeking not implemented in this simple example
        }

        private void EqBand_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int bands = EqualizerSampleProvider.Frequencies.Length;
            var gains = new float[bands];
            for (int i = 0; i < bands; i++)
            {
                var ctrl = this.FindName($"Band{i}") as Slider;
                gains[i] = ctrl != null ? (float)ctrl.Value : 0f;
            }
            player.UpdateEqGains(gains);
            try
            {
                userSettings.EqGains = (float[])gains.Clone();
                ScheduleSettingsSave();
            }
            catch { }
        }

        private void EffectToggled(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as System.Windows.Controls.Button;
                if (btn == null) return;
                // toggle indicator attached property
                bool current = IndicatorHelper.GetIsIndicatorOn(btn);
                IndicatorHelper.SetIsIndicatorOn(btn, !current);

                if (btn == BtnEcho)
                {
                    bool newEnabled = !current;
                    // enable/disable slider and notify player
                    SliderEcho.IsEnabled = newEnabled;
                    player.EnableEcho(newEnabled);
                    userSettings.EchoEnabled = newEnabled;
                    ScheduleSettingsSave();
                }
                else if (btn == BtnReverb)
                {
                    bool newEnabled = !current;
                    SliderReverb.IsEnabled = newEnabled;
                    player.EnableReverb(newEnabled);
                    userSettings.ReverbEnabled = newEnabled;
                    ScheduleSettingsSave();
                }
                else if (btn == BtnStereo)
                {
                    bool newEnabled = !current;
                    SliderStereo.IsEnabled = newEnabled;
                    player.EnableStereo(newEnabled);
                    userSettings.StereoEnabled = newEnabled;
                    ScheduleSettingsSave();
                }
                else if (btn == BtnBass)
                {
                    bool newEnabled = !current;
                    SliderBass.IsEnabled = newEnabled;
                    player.EnableBass(newEnabled);
                    userSettings.BassEnabled = newEnabled;
                    ScheduleSettingsSave();
                }
                else if (btn == BtnTreble)
                {
                    bool newEnabled = !current;
                    SliderTreble.IsEnabled = newEnabled;
                    player.EnableTreble(newEnabled);
                    userSettings.TrebleEnabled = newEnabled;
                    ScheduleSettingsSave();
                }
                // TODO: wire these flags into audio pipeline when implemented
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            // ensure timers stopped and pending deletes finalized, then dispose audio
            try
            {
                uiTimer.Stop();
                marqueeTimer.Stop();
                markedTimer.Stop();
                // perform any pending deletes synchronously to avoid leftover temp state
                if (pendingDeleteIndex.HasValue)
                {
                    int idx = pendingDeleteIndex.Value;
                    pendingDeleteIndex = null;
                    try
                    {
                        if (idx >= 0 && idx < tracks.Count)
                        {
                            var path = tracks[idx];
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            tracks.RemoveAt(idx);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                // flush any pending settings save
                try
                {
                    settingsSaveTimer.Stop();
                    SettingsService.Save(userSettings);
                }
                catch { }
                player.Dispose();
                base.OnClosed(e);
                System.Windows.Application.Current.Shutdown();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Only allow close via power button
            if (!allowClose)
            {
                e.Cancel = true;
                return;
            }
            base.OnClosing(e);
        }
    }
}
