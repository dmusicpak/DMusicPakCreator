using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using DMusicPakCreator.ViewModels;
using Microsoft.UI.Xaml.Controls;
using DMusicPakDotNet;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using WinRT.Interop;
using System.Text.RegularExpressions;

namespace DMusicPakCreator.Views;

public sealed partial class CreatorPage : Page
{
    private Package _currentPackage;
    private MediaPlayer _mediaPlayer;
    private string _currentFilePath;
    private bool _isModified;
    private byte[] _tempAudioData;
    private byte[] _tempCoverData;
    private CoverFormat _tempCoverFormat;
    private uint _tempCoverWidth;
    private uint _tempCoverHeight;
    private bool _isPlaying;
    private DispatcherTimer _playbackTimer;
    private List<LyricLine> _parsedLyrics;
    private int _currentLyricIndex = -1;

    public CreatorViewModel ViewModel { get; }

    public CreatorPage()
    {
        ViewModel = App.GetService<CreatorViewModel>();
        InitializeComponent();

        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        _mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;

        _playbackTimer = new DispatcherTimer();
        _playbackTimer.Interval = TimeSpan.FromMilliseconds(100);
        _playbackTimer.Tick += PlaybackTimer_Tick;

        _parsedLyrics = new List<LyricLine>();

        UpdateUIState();
        UpdateStatusText("就绪");
    }

    #region File Operations

    private async void NewPackage_Click(object sender, RoutedEventArgs e)
    {
        if (!await PromptSaveChanges()) return;

        try
        {
            _currentPackage?.Dispose();
            _currentPackage = new Package();
            _currentFilePath = null;
            _isModified = false;
            _tempAudioData = null;
            _tempCoverData = null;

            ClearForm();
            UpdateUIState();
            UpdateStatusText("新建音乐包");
            ShowInfoBar("新建成功", "已创建新的音乐包，请导入音频文件", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfoBar("创建失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        if (!await PromptSaveChanges()) return;

        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        
        picker.FileTypeFilter.Add(".dmusicpak");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await LoadPackage(file.Path);
        }
    }

    private async Task LoadPackage(string path)
    {
        try
        {
            UpdateStatusText("正在加载...");

            _currentPackage?.Dispose();
            _currentPackage = new Package(path);
            _currentFilePath = path;
            _isModified = false;

            await LoadPackageData();
            UpdateUIState();
            UpdateStatusText($"已加载: {Path.GetFileName(path)}");
            ShowInfoBar("打开成功", $"已加载: {Path.GetFileName(path)}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            UpdateStatusText("加载失败");
            ShowInfoBar("打开失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void SavePackage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPackage == null) return;

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            await SavePackageAs();
        }
        else
        {
            await SavePackage(_currentFilePath);
        }
    }

    private async void SavePackageAs_Click(object sender, RoutedEventArgs e)
    {
        await SavePackageAs();
    }

    private async Task SavePackageAs()
    {
        var picker = new FileSavePicker();
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeChoices.Add("DMusicPak文件", new[] { ".dmusicpak" });
        picker.SuggestedFileName = string.IsNullOrEmpty(TitleBox.Text) ? "音乐包" : TitleBox.Text;

        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            await SavePackage(file.Path);
        }
    }

    private async Task SavePackage(string path)
    {
        try
        {
            UpdateStatusText("正在保存...");
            
            // 保存所有数据
            SaveFormData();
            
            // 确保音频数据被保存
            if (_tempAudioData != null && _tempAudioData.Length > 0)
            {
                var audio = new Audio
                {
                    SourceFilename = AudioFileNameText.Text,
                    Data = _tempAudioData
                };
                _currentPackage.SetAudio(audio);
            }
            
            // 确保封面数据被保存
            if (_tempCoverData != null && _tempCoverData.Length > 0)
            {
                var cover = new Cover
                {
                    Format = _tempCoverFormat,
                    Data = _tempCoverData,
                    Width = _tempCoverWidth,
                    Height = _tempCoverHeight
                };
                _currentPackage.SetCover(cover);
            }
            
            _currentPackage.Save(path);
            _currentFilePath = path;
            _isModified = false;

            UpdateUIState();
            UpdateStatusText($"已保存: {Path.GetFileName(path)}");
            ShowInfoBar("保存成功", $"已保存到: {Path.GetFileName(path)}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            UpdateStatusText("保存失败");
            ShowInfoBar("保存失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task<bool> PromptSaveChanges()
    {
        if (!_isModified || _currentPackage == null) return true;

        var dialog = new ContentDialog
        {
            Title = "保存更改",
            Content = "当前文件已修改，是否保存?",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "不保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                await SavePackageAs();
            }
            else
            {
                await SavePackage(_currentFilePath);
            }

            return true;
        }

        return result == ContentDialogResult.Secondary;
    }

    #endregion

    #region Audio Operations

    private async void ImportAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPackage == null)
        {
            ShowInfoBar("提示", "请先创建或打开一个音乐包", InfoBarSeverity.Warning);
            return;
        }

        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".flac");
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".ogg");
        picker.FileTypeFilter.Add(".m4a");
        picker.FileTypeFilter.Add(".aac");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await ImportAudioFile(file);
        }
    }

    private async void AudioDrop_DragOver(object sender, DragEventArgs e)
    {
        if (_currentPackage == null)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "导入音频文件";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }
    }

    private async void AudioDrop_Drop(object sender, DragEventArgs e)
    {
        if (_currentPackage == null) return;

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                string ext = file.FileType.ToLower();
                if (ext == ".mp3" || ext == ".flac" || ext == ".wav" || 
                    ext == ".ogg" || ext == ".m4a" || ext == ".aac")
                {
                    await ImportAudioFile(file);
                }
                else
                {
                    ShowInfoBar("格式错误", "请拖拽音频文件", InfoBarSeverity.Warning);
                }
            }
        }
    }

    private async Task ImportAudioFile(StorageFile file)
    {
        try
        {
            UpdateStatusText("正在导入音频...");

            var buffer = await FileIO.ReadBufferAsync(file);
            _tempAudioData = new byte[buffer.Length];
            using (var reader = DataReader.FromBuffer(buffer))
            {
                reader.ReadBytes(_tempAudioData);
            }

            _isModified = true;

            // 更新UI
            AudioFileNameText.Text = file.Name;
            AudioSizeText.Text = FormatFileSize(_tempAudioData.Length);
            AudioEmptyState.Visibility = Visibility.Collapsed;
            AudioInfoState.Visibility = Visibility.Visible;
            ImportAudioButton.Visibility = Visibility.Collapsed;

            // 自动识别和填充元数据
            await AutoFillMetadata(file);

            // 加载音频用于播放
            await LoadAudioForPlayback(file);

            UpdateUIState();
            UpdateStatusText($"已导入: {file.Name}");
            ShowInfoBar("导入成功", $"已导入音频: {file.Name}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            UpdateStatusText("导入失败");
            ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task AutoFillMetadata(StorageFile file)
    {
        try
        {
            var props = await file.Properties.GetMusicPropertiesAsync();

            if (!string.IsNullOrEmpty(props.Title) && string.IsNullOrEmpty(TitleBox.Text))
                TitleBox.Text = props.Title;
            if (!string.IsNullOrEmpty(props.Artist) && string.IsNullOrEmpty(ArtistBox.Text))
                ArtistBox.Text = props.Artist;
            if (!string.IsNullOrEmpty(props.Album) && string.IsNullOrEmpty(AlbumBox.Text))
                AlbumBox.Text = props.Album;
            if (props.Year > 0 && string.IsNullOrEmpty(YearBox.Text))
                YearBox.Text = props.Year.ToString();

            if (props.Duration.TotalMilliseconds > 0)
            {
                DurationBox.Text = ((uint)props.Duration.TotalMilliseconds).ToString();
            }

            if (props.Bitrate > 0)
            {
                BitrateBox.Text = (props.Bitrate / 1000).ToString();
            }
        }
        catch
        {
            // 静默失败
        }
    }

    private async Task LoadAudioForPlayback(StorageFile file)
    {
        try
        {
            var stream = await file.OpenAsync(FileAccessMode.Read);
            _mediaPlayer.Source = MediaSource.CreateFromStream(stream, file.ContentType);
            PlayPauseButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ShowInfoBar("音频加载失败", ex.Message, InfoBarSeverity.Warning);
        }
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isPlaying)
        {
            _mediaPlayer.Pause();
            _isPlaying = false;
            PlayPauseIcon.Glyph = "\uE768"; // Play
            _playbackTimer.Stop();
        }
        else
        {
            _mediaPlayer.Play();
            _isPlaying = true;
            PlayPauseIcon.Glyph = "\uE769"; // Pause
            _playbackTimer.Start();
        }
    }

    private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _isPlaying = false;
            PlayPauseIcon.Glyph = "\uE768";
            _playbackTimer.Stop();
            TimeDisplay.Text = "00:00 / " + FormatTime(_mediaPlayer.NaturalDuration);
            _currentLyricIndex = -1;
            UpdateLyricsHighlight();
        });
    }

    private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TimeDisplay.Text = "00:00 / " + FormatTime(sender.NaturalDuration);
        });
    }

    private void PlaybackTimer_Tick(object sender, object e)
    {
        if (_mediaPlayer.PlaybackSession != null)
        {
            var current = _mediaPlayer.PlaybackSession.Position;
            var total = _mediaPlayer.NaturalDuration;
            TimeDisplay.Text = FormatTime(current) + " / " + FormatTime(total);
            
            // 更新歌词显示
            UpdateCurrentLyric(current);
        }
    }

    #endregion

    #region Cover Operations

    private async void ImportCover_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPackage == null)
        {
            ShowInfoBar("提示", "请先创建或打开一个音乐包", InfoBarSeverity.Warning);
            return;
        }

        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".webp");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            await ImportCoverFile(file);
        }
    }

    private async void CoverDrop_DragOver(object sender, DragEventArgs e)
    {
        if (_currentPackage == null)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "导入封面图片";
            e.DragUIOverride.IsCaptionVisible = true;
        }
    }

    private async void CoverDrop_Drop(object sender, DragEventArgs e)
    {
        if (_currentPackage == null) return;

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                string ext = file.FileType.ToLower();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" ||
                     ext == ".bmp" || ext == ".webp")
                {
                    await ImportCoverFile(file);
                }
                else
                {
                    ShowInfoBar("格式错误", "请拖拽图片文件", InfoBarSeverity.Warning);
                }
            }
        }
    }

    private async Task ImportCoverFile(StorageFile file)
    {
        try
        {
            UpdateStatusText("正在导入封面...");

            var buffer = await FileIO.ReadBufferAsync(file);
            _tempCoverData = new byte[buffer.Length];
            using (var reader = DataReader.FromBuffer(buffer))
            {
                reader.ReadBytes(_tempCoverData);
            }

            _tempCoverFormat = DetectImageFormat(file.FileType);
            var props = await file.Properties.GetImagePropertiesAsync();
            _tempCoverWidth = props.Width;
            _tempCoverHeight = props.Height;

            _isModified = true;

            // 显示封面
            var bitmap = new BitmapImage();
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                await bitmap.SetSourceAsync(stream);
            }

            CoverImage.Source = bitmap;
            CoverImage.Visibility = Visibility.Visible;
            CoverEmptyState.Visibility = Visibility.Collapsed;
            CoverInfoText.Text = $"{props.Width} × {props.Height} • {_tempCoverFormat} • {FormatFileSize(_tempCoverData.Length)}";
            RemoveCoverButton.IsEnabled = true;

            UpdateUIState();
            UpdateStatusText("已导入封面");
            ShowInfoBar("导入成功", "已导入封面图片", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            UpdateStatusText("导入失败");
            ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void RemoveCover_Click(object sender, RoutedEventArgs e)
    {
        CoverImage.Source = null;
        CoverImage.Visibility = Visibility.Collapsed;
        CoverEmptyState.Visibility = Visibility.Visible;
        CoverInfoText.Text = "";
        RemoveCoverButton.IsEnabled = false;
        _tempCoverData = null;
        _isModified = true;
        UpdateUIState();
        ShowInfoBar("已移除", "已移除封面图片", InfoBarSeverity.Informational);
    }

    private CoverFormat DetectImageFormat(string fileExtension)
    {
        return fileExtension.ToLower() switch
        {
            ".jpg" or ".jpeg" => CoverFormat.JPEG,
            ".png" => CoverFormat.PNG,
            ".webp" => CoverFormat.WebP,
            ".bmp" => CoverFormat.BMP,
            _ => CoverFormat.JPEG
        };
    }

    #endregion

    #region Lyrics Operations

    private class LyricLine
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; }
        public TextBlock TextBlock { get; set; }
    }

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".lrc");
        picker.FileTypeFilter.Add(".srt");
        picker.FileTypeFilter.Add(".ass");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                string content = await FileIO.ReadTextAsync(file);
                LyricsBox.Text = content;

                // 自动识别格式
                string ext = file.FileType.ToLower();
                if (ext == ".lrc") LyricsFormatCombo.SelectedIndex = 3;
                else if (ext == ".srt") LyricsFormatCombo.SelectedIndex = 4;
                else if (ext == ".ass") LyricsFormatCombo.SelectedIndex = 5;

                ParseAndDisplayLyrics();
                ShowInfoBar("导入成功", $"已导入歌词: {file.Name}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
            }
        }
    }

    private void ClearLyrics_Click(object sender, RoutedEventArgs e)
    {
        LyricsBox.Text = string.Empty;
        LyricsFormatCombo.SelectedIndex = 0;
        _parsedLyrics.Clear();
        UpdateLyricsPreview();
    }

    private void LyricsFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ParseAndDisplayLyrics();
    }

    private void ParseAndDisplayLyrics()
    {
        _parsedLyrics = new List<LyricLine>();
        _parsedLyrics.Clear();
        _currentLyricIndex = -1;

        if (LyricsBox != null && string.IsNullOrWhiteSpace(LyricsBox.Text))
        {
            UpdateLyricsPreview();
            return;
        }

        int format = LyricsFormatCombo.SelectedIndex;
        
        // 解析LRC格式 (索引1-3都是LRC)
        if (format >= 1 && format <= 3)
        {
            ParseLrcLyrics(LyricsBox.Text);
        }
        
        UpdateLyricsPreview();
    }

    private void ParseLrcLyrics(string lrcText)
    {
        // LRC 格式: [mm:ss.xx]歌词文本
        var regex = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2})\](.*)");
        var lines = lrcText.Split('\n');

        foreach (var line in lines)
        {
            var match = regex.Match(line.Trim());
            if (match.Success)
            {
                int minutes = int.Parse(match.Groups[1].Value);
                int seconds = int.Parse(match.Groups[2].Value);
                int centiseconds = int.Parse(match.Groups[3].Value);
                string text = match.Groups[4].Value.Trim();

                var time = new TimeSpan(0, 0, minutes, seconds, centiseconds * 10);
                _parsedLyrics.Add(new LyricLine
                {
                    Time = time,
                    Text = text
                });
            }
        }

        // 按时间排序
        _parsedLyrics = _parsedLyrics.OrderBy(l => l.Time).ToList();
    }

    private void UpdateLyricsPreview()
    {
        if (LyricsPreviewPanel == null) return;
        LyricsPreviewPanel.Children.Clear();

        if (_parsedLyrics.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "无歌词",
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 200, 0, 0)
            };
            LyricsPreviewPanel.Children.Add(emptyText);
            return;
        }

        foreach (var lyric in _parsedLyrics)
        {
            var textBlock = new TextBlock
            {
                Text = lyric.Text,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 4, 0, 4)
            };
            lyric.TextBlock = textBlock;
            LyricsPreviewPanel.Children.Add(textBlock);
        }
    }

    private void UpdateCurrentLyric(TimeSpan currentTime)
    {
        if (_parsedLyrics.Count == 0) return;

        // 找到当前应该显示的歌词
        int newIndex = -1;
        for (int i = 0; i < _parsedLyrics.Count; i++)
        {
            if (_parsedLyrics[i].Time <= currentTime)
            {
                newIndex = i;
            }
            else
            {
                break;
            }
        }

        if (newIndex != _currentLyricIndex)
        {
            _currentLyricIndex = newIndex;
            UpdateLyricsHighlight();
        }
    }

    private void UpdateLyricsHighlight()
    {
        for (int i = 0; i < _parsedLyrics.Count; i++)
        {
            var lyric = _parsedLyrics[i];
            if (lyric.TextBlock != null)
            {
                if (i == _currentLyricIndex)
                {
                    // 高亮当前歌词
                    lyric.TextBlock.Foreground = new SolidColorBrush(Colors.White);
                    lyric.TextBlock.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
                    lyric.TextBlock.FontSize = 16;
                    
                    // 更新时间显示
                    CurrentLyricTimeText.Text = FormatTime(lyric.Time);
                    
                    // 滚动到当前歌词
                    ScrollToLyric(lyric.TextBlock);
                }
                else
                {
                    // 其他歌词变暗
                    lyric.TextBlock.Foreground = new SolidColorBrush(Colors.Gray);
                    lyric.TextBlock.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                    lyric.TextBlock.FontSize = 14;
                }
            }
        }
    }

    private void ScrollToLyric(TextBlock textBlock)
    {
        try
        {
            var transform = textBlock.TransformToVisual(LyricsScrollViewer);
            var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            
            // 滚动到使当前歌词在中间位置
            var targetOffset = LyricsScrollViewer.VerticalOffset + position.Y - (LyricsScrollViewer.ActualHeight / 2);
            targetOffset = Math.Max(0, targetOffset);
            
            LyricsScrollViewer.ChangeView(null, targetOffset, null, false);
        }
        catch
        {
            // 忽略滚动错误
        }
    }

    #endregion

    #region Data Management

    private async Task LoadPackageData()
    {
        try
        {
            // 加载元数据
            var metadata = _currentPackage.GetMetadata();
            TitleBox.Text = metadata.Title;
            ArtistBox.Text = metadata.Artist;
            AlbumBox.Text = metadata.Album;
            GenreBox.Text = metadata.Genre;
            YearBox.Text = metadata.Year;
            CommentBox.Text = metadata.Comment;
            DurationBox.Text = metadata.DurationMs.ToString();
            BitrateBox.Text = metadata.Bitrate.ToString();
            SampleRateBox.Text = metadata.SampleRate.ToString();
            ChannelsBox.Text = metadata.Channels.ToString();

            // 加载音频
            var audio = _currentPackage.GetAudio();
            if (audio?.Data != null && audio.Data.Length > 0)
            {
                AudioFileNameText.Text = audio.SourceFilename;
                AudioSizeText.Text = FormatFileSize(audio.Data.Length);
                AudioEmptyState.Visibility = Visibility.Collapsed;
                AudioInfoState.Visibility = Visibility.Visible;
                ImportAudioButton.Visibility = Visibility.Collapsed;
                _tempAudioData = audio.Data;

                try
                {
                    var tempFile = await CreateTempAudioFile(audio);
                    await LoadAudioForPlayback(tempFile);
                }
                catch
                {
                }
            }

            // 加载歌词
            var lyrics = _currentPackage.GetLyrics();
            if (lyrics?.Data != null && lyrics.Data.Length > 0)
            {
                LyricsBox.Text = lyrics.Text;
                LyricsFormatCombo.SelectedIndex = (int)lyrics.Format;
                ParseAndDisplayLyrics();
            }

            // 加载封面
            var cover = _currentPackage.GetCover();
            if (cover?.Data != null && cover.Data.Length > 0)
            {
                _tempCoverData = cover.Data;
                _tempCoverFormat = cover.Format;
                _tempCoverWidth = cover.Width;
                _tempCoverHeight = cover.Height;

                var bitmap = new BitmapImage();
                using (var ms = new MemoryStream(cover.Data))
                {
                    var stream = ms.AsRandomAccessStream();
                    await bitmap.SetSourceAsync(stream);
                }

                CoverImage.Source = bitmap;
                CoverImage.Visibility = Visibility.Visible;
                CoverEmptyState.Visibility = Visibility.Collapsed;
                CoverInfoText.Text = $"{cover.Width} × {cover.Height} • {cover.Format} • {FormatFileSize(cover.Data.Length)}";
                RemoveCoverButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            ShowInfoBar("加载数据失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task<StorageFile> CreateTempAudioFile(Audio audio)
    {
        var tempFolder = ApplicationData.Current.TemporaryFolder;
        var tempFile = await tempFolder.CreateFileAsync(
            audio.SourceFilename,
            CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteBytesAsync(tempFile, audio.Data);
        return tempFile;
    }

    private void SaveFormData()
    {
        if (_currentPackage == null) return;

        try
        {
            // 保存元数据
            var metadata = new Metadata
            {
                Title = TitleBox.Text,
                Artist = ArtistBox.Text,
                Album = AlbumBox.Text,
                Genre = GenreBox.Text,
                Year = YearBox.Text,
                Comment = CommentBox.Text
            };

            if (uint.TryParse(DurationBox.Text, out uint duration))
                metadata.DurationMs = duration;
            if (uint.TryParse(BitrateBox.Text, out uint bitrate))
                metadata.Bitrate = bitrate;
            if (uint.TryParse(SampleRateBox.Text, out uint sampleRate))
                metadata.SampleRate = sampleRate;
            if (ushort.TryParse(ChannelsBox.Text, out ushort channels))
                metadata.Channels = channels;

            _currentPackage.SetMetadata(metadata);

            // 保存歌词
            if (!string.IsNullOrWhiteSpace(LyricsBox.Text))
            {
                var lyrics = new Lyrics
                {
                    Format = (LyricFormat)LyricsFormatCombo.SelectedIndex,
                    Data = System.Text.Encoding.UTF8.GetBytes(LyricsBox.Text)
                };
                _currentPackage.SetLyrics(lyrics);
            }
        }
        catch (Exception ex)
        {
            ShowInfoBar("保存数据失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void ClearForm()
    {
        TitleBox.Text = string.Empty;
        ArtistBox.Text = string.Empty;
        AlbumBox.Text = string.Empty;
        GenreBox.Text = string.Empty;
        YearBox.Text = string.Empty;
        CommentBox.Text = string.Empty;
        DurationBox.Text = string.Empty;
        BitrateBox.Text = string.Empty;
        SampleRateBox.Text = string.Empty;
        ChannelsBox.Text = string.Empty;
        LyricsBox.Text = string.Empty;

        AudioFileNameText.Text = "未导入";
        AudioSizeText.Text = "0 KB";
        AudioEmptyState.Visibility = Visibility.Visible;
        AudioInfoState.Visibility = Visibility.Collapsed;
        ImportAudioButton.Visibility = Visibility.Visible;

        CoverImage.Source = null;
        CoverImage.Visibility = Visibility.Collapsed;
        CoverEmptyState.Visibility = Visibility.Visible;
        CoverInfoText.Text = "";
        RemoveCoverButton.IsEnabled = false;

        LyricsFormatCombo.SelectedIndex = 0;
        _parsedLyrics.Clear();
        UpdateLyricsPreview();

        PlayPauseButton.IsEnabled = false;
        TimeDisplay.Text = "00:00 / 00:00";
    }

    private void OnFormChanged(object sender, TextChangedEventArgs e)
    {
        if (_currentPackage != null)
        {
            _isModified = true;
            UpdateUIState();
        }
        
        // 如果是歌词框变化，重新解析
        if (sender == LyricsBox)
        {
            ParseAndDisplayLyrics();
        }
    }

    #endregion

    #region UI Helpers

    private void UpdateUIState()
    {
        bool hasPackage = _currentPackage != null;

        SaveButton.IsEnabled = hasPackage && _isModified;
        SaveAsButton.IsEnabled = hasPackage;

        string title = "DMusicPak Creator";
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            title += $" - {Path.GetFileName(_currentFilePath)}";
        }
        else if (hasPackage)
        {
            title += " - 未命名";
        }

        if (_isModified)
        {
            title += " *";
        }

        App.GetService<ShellViewModel>().SetTitle(title);
    }

    private void UpdateStatusText(string text)
    {
        StatusText.Text = text;
    }

    private void ShowInfoBar(string title, string message, InfoBarSeverity severity)
    {
        InfoBar.Title = title;
        InfoBar.Message = message;
        InfoBar.Severity = severity;
        InfoBar.IsOpen = true;
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    private string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return time.ToString(@"h\:mm\:ss");
        return time.ToString(@"mm\:ss");
    }

    #endregion

    private async void Window_Closed(object sender, WindowEventArgs args)
    {
        if (_isModified && _currentPackage != null)
        {
            args.Handled = true;
            if (await PromptSaveChanges())
            {
                _mediaPlayer?.Dispose();
                _currentPackage?.Dispose();
                _playbackTimer?.Stop();
                Application.Current.Exit();
            }
        }
        else
        {
            _mediaPlayer?.Dispose();
            _currentPackage?.Dispose();
            _playbackTimer?.Stop();
        }
    }
}