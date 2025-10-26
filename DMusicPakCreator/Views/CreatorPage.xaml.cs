using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using DMusicPakCreator.ViewModels;
using DMusicPakCreator.Services;
using DMusicPakDotNet;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace DMusicPakCreator.Views;

public sealed partial class CreatorPage : Page
{
    private DispatcherTimer _playbackTimer;

    public CreatorViewModel ViewModel { get; }

    public CreatorPage()
    {
        // 获取服务
        var packageService = App.GetService<PackageService>();
        var audioService = App.GetService<AudioService>();
        var coverService = App.GetService<CoverService>();
        var lyricsService = App.GetService<LyricsService>();
        var mediaPlayerService = App.GetService<MediaPlayerService>();

        ViewModel = new CreatorViewModel(
            packageService,
            audioService,
            coverService,
            lyricsService,
            mediaPlayerService);

        InitializeComponent();

        // 初始化播放定时器
        _playbackTimer = new DispatcherTimer();
        _playbackTimer.Interval = TimeSpan.FromMilliseconds(100);
        _playbackTimer.Tick += PlaybackTimer_Tick;

        // 绑定ViewModel到DataContext
        DataContext = ViewModel;
    }

    #region File Operations

    private async void NewPackage_Click(object sender, RoutedEventArgs e)
    {
        if (!await PromptSaveChanges()) return;

        try
        {
            ViewModel.CreateNewPackageCommand.Execute(null);
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
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeFilter.Add(".dmusicpak");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                await ViewModel.LoadPackageCommand.ExecuteAsync(file.Path);
                ShowInfoBar("打开成功", $"已加载: {file.Name}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowInfoBar("打开失败", ex.Message, InfoBarSeverity.Error);
            }
        }
    }

    private async void SavePackage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.CurrentFilePath))
        {
            await SavePackageAs();
        }
        else
        {
            await SavePackage(ViewModel.CurrentFilePath);
        }
    }

    private async void SavePackageAs_Click(object sender, RoutedEventArgs e)
    {
        await SavePackageAs();
    }

    private async Task SavePackageAs()
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.FileTypeChoices.Add("DMusicPak文件", new[] { ".dmusicpak" });
        picker.SuggestedFileName = string.IsNullOrEmpty(ViewModel.Title) ? "音乐包" : ViewModel.Title;

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
            ViewModel.SavePackageCommand.Execute(path);
            ShowInfoBar("保存成功", $"已保存到: {Path.GetFileName(path)}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfoBar("保存失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task<bool> PromptSaveChanges()
    {
        if (!ViewModel.IsModified) return true;

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
            if (string.IsNullOrEmpty(ViewModel.CurrentFilePath))
            {
                await SavePackageAs();
            }
            else
            {
                await SavePackage(ViewModel.CurrentFilePath);
            }
            return true;
        }

        return result == ContentDialogResult.Secondary;
    }

    #endregion

    #region Audio Operations

    private async void ImportAudio_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".flac");
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".ogg");
        picker.FileTypeFilter.Add(".m4a");
        picker.FileTypeFilter.Add(".aac");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                await ViewModel.ImportAudioAsync(file);
                ShowInfoBar("导入成功", $"已导入音频: {file.Name}", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
            }
        }
    }

    private async void AudioDrop_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "导入音频文件";
        }
    }

    private async void AudioDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                string ext = file.FileType.ToLower();
                if (ext == ".mp3" || ext == ".flac" || ext == ".wav" ||
                    ext == ".ogg" || ext == ".m4a" || ext == ".aac")
                {
                    try
                    {
                        await ViewModel.ImportAudioAsync(file);
                        ShowInfoBar("导入成功", $"已导入音频: {file.Name}", InfoBarSeverity.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
                    }
                }
                else
                {
                    ShowInfoBar("格式错误", "请拖拽音频文件", InfoBarSeverity.Warning);
                }
            }
        }
    }

    #endregion

    #region Cover Operations

    private async void ImportCover_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".webp");

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                await ViewModel.ImportCoverAsync(file);
                ShowInfoBar("导入成功", "已导入封面图片", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
            }
        }
    }

    private async void CoverDrop_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "导入封面图片";
        }
    }

    private async void CoverDrop_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0 && items[0] is StorageFile file)
            {
                string ext = file.FileType.ToLower();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" ||
                    ext == ".bmp" || ext == ".webp")
                {
                    try
                    {
                        await ViewModel.ImportCoverAsync(file);
                        ShowInfoBar("导入成功", "已导入封面图片", InfoBarSeverity.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
                    }
                }
                else
                {
                    ShowInfoBar("格式错误", "请拖拽图片文件", InfoBarSeverity.Warning);
                }
            }
        }
    }

    private void RemoveCover_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveCoverCommand.Execute(null);
        ShowInfoBar("已移除", "已移除封面图片", InfoBarSeverity.Informational);
    }

    #endregion

    #region Lyrics Operations

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

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
        ViewModel.ClearLyricsCommand.Execute(null);
    }

    private void LyricsFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.LyricsFormat = (LyricFormat)LyricsFormatCombo.SelectedIndex;
            ViewModel.ParseLyricsCommand.Execute(null);
        }
    }

    private void UpdateLyricsHighlight()
    {
        // 更新歌词高亮显示
        if (LyricsPreviewPanel == null || ViewModel.ParsedLyrics == null)
            return;

        for (int i = 0; i < ViewModel.ParsedLyrics.Count; i++)
        {
            if (i < LyricsPreviewPanel.Children.Count)
            {
                var textBlock = LyricsPreviewPanel.Children[i] as TextBlock;
                if (textBlock != null)
                {
                    if (i == ViewModel.CurrentLyricIndex)
                    {
                        textBlock.Foreground = new SolidColorBrush(Colors.White);
                        textBlock.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
                        textBlock.FontSize = 16;
                        ScrollToLyric(textBlock);
                    }
                    else
                    {
                        textBlock.Foreground = new SolidColorBrush(Colors.Gray);
                        textBlock.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                        textBlock.FontSize = 14;
                    }
                }
            }
        }
    }

    private void ScrollToLyric(TextBlock textBlock)
    {
        try
        {
            if (textBlock == null || LyricsScrollViewer == null)
                return;

            var transform = textBlock.TransformToVisual(LyricsScrollViewer);
            var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            var targetOffset = LyricsScrollViewer.VerticalOffset + position.Y - (LyricsScrollViewer.ActualHeight / 2);
            targetOffset = Math.Max(0, targetOffset);
            LyricsScrollViewer.ChangeView(null, targetOffset, null, false);
        }
        catch { }
    }

    #endregion

    #region Playback

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.TogglePlayPauseCommand.Execute(null);
        
        if (ViewModel.IsPlaying)
        {
            _playbackTimer.Start();
        }
        else
        {
            _playbackTimer.Stop();
        }
    }

    private void PlaybackTimer_Tick(object sender, object e)
    {
        ViewModel.UpdatePlaybackTime();
        UpdateLyricsHighlight();
    }

    #endregion

    #region UI Helpers

    private void ShowInfoBar(string title, string message, InfoBarSeverity severity)
    {
        if (InfoBar == null) return;

        InfoBar.Title = title;
        InfoBar.Message = message;
        InfoBar.Severity = severity;
        InfoBar.IsOpen = true;
    }

    private void OnFormChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsModified = true;
        }
    }

    #endregion
}