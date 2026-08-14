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

namespace Mp3Player
{
    public partial class MainWindow : Window
    {
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
        // Manage-audio state
        private bool manageEnabled = false;
        private int? pendingDeleteIndex = null;
        private int pendingRenameIndex = -1;
        private string? pendingRenameName = null;
        private double rightPanelWidth = 480;

        public MainWindow()
        {
            InitializeComponent();

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

            SliderVolume.Value = 0.8;
            // ensure play indicator initial state
            SetButtonIndicator(BtnPlayPause, false);
            UpdatePowerIndicator();
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

        private void ScheduleTrackListHighlightRefresh()
        {
            Dispatcher.BeginInvoke(new Action(RefreshTrackListHighlights), DispatcherPriority.Loaded);
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
                    prevLeft = this.Left; prevTop = this.Top; prevWidth = this.Width; prevHeight = this.Height;
                    var wa = SystemParameters.WorkArea;
                    this.Left = wa.Left; this.Top = wa.Top; this.Width = wa.Width; this.Height = wa.Height;
                    isMaximized = true;
                }
                else
                {
                    this.Left = prevLeft; this.Top = prevTop; this.Width = prevWidth; this.Height = prevHeight;
                    isMaximized = false;
                }
            }
            catch { }
        }

        private bool echoEnabled = false;
        private bool reverbEnabled = false;
        private bool stereoEnabled = false;


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
            // simple visualization: draw bars from peak values
            Dispatcher.Invoke(() =>
            {
                SpectrumCanvas.Children.Clear();
                if (e.Volumes == null) return;
                int bands = EqualizerSampleProvider.Frequencies.Length; // match EQ band count
                const int bulbsPerBand = 9; // 4 green, 3 amber, 2 red
                const int greenCount = 4;
                const int amberCount = 3;
                const int redCount = 2;
                double spacing = 6.0; // gap between bands
                double totalSpacing = spacing * (bands - 1);
                double bandWidth = bands > 0 ? Math.Max(20, (SpectrumCanvas.ActualWidth - totalSpacing) / bands) : 20;
                double bulbSpacing = 4.0; // gap between bulbs vertically
                double totalBulbSpacing = bulbSpacing * (bulbsPerBand - 1);
                double bulbHeight = Math.Max(4, (SpectrumCanvas.ActualHeight - totalBulbSpacing) / bulbsPerBand);
                double bulbWidth = Math.Max(8, bandWidth * 0.7);
                double leftOffset = (bandWidth - bulbWidth) / 2.0;

                for (int i = 0; i < bands; i++)
                {
                    double level = 0;
                    // e.Volumes contains per-band magnitudes from FFT (SampleAggregator)
                    if (e.Volumes != null && i < e.Volumes.Length) level = e.Volumes[i];
                    level = Math.Max(0.0, Math.Min(1.0, level));

                    // determine how many bulbs light up (0..bulbsPerBand)
                    int lit = (int)Math.Round(level * bulbsPerBand);
                    if (lit < 0) lit = 0; if (lit > bulbsPerBand) lit = bulbsPerBand;

                    for (int b = 0; b < bulbsPerBand; b++)
                    {
                        // bottom bulb index 0 -> bottom of canvas
                        double x = i * (bandWidth + spacing) + leftOffset;
                        double y = SpectrumCanvas.ActualHeight - ((b + 1) * bulbHeight + b * bulbSpacing);

                        bool on = b < lit;
                        Brush fill;
                        if (!on) fill = new SolidColorBrush(Color.FromRgb(40, 40, 40)); // unlit dark
                        else if (b < greenCount) fill = Brushes.LimeGreen;
                        else if (b < greenCount + amberCount) fill = Brushes.Orange;
                        else fill = Brushes.Red;

                        var rect = new Rectangle
                        {
                            Width = bulbWidth,
                            Height = bulbHeight,
                            Fill = fill,
                            RadiusX = 2,
                            RadiusY = 2,
                            Stroke = Brushes.Black,
                            StrokeThickness = 1
                        };

                        Canvas.SetLeft(rect, x);
                        Canvas.SetTop(rect, y);
                        SpectrumCanvas.Children.Add(rect);
                    }
                }
            });
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
            var files = Directory.GetFiles(folder, "*.mp3").OrderBy(f => f).ToArray();
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
            int displayLen = 30;
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
                    echoEnabled = !current;
                }
                else if (btn == BtnReverb)
                {
                    reverbEnabled = !current;
                }
                else if (btn == BtnStereo)
                {
                    stereoEnabled = !current;
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
