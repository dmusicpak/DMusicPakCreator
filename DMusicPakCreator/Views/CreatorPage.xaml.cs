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
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DMusicPakCreator.Views;

public sealed partial class CreatorPage : Page
{
    private DispatcherTimer _playbackTimer;

    public CreatorViewModel ViewModel { get; }

    public CreatorPage()
    {
        ViewModel = new CreatorViewModel(
            App.GetService<PackageService>(),
            App.GetService<AudioService>(),
            App.GetService<CoverService>(),
            App.GetService<LyricsService>(),
            App.GetService<MediaPlayerService>());

        InitializeComponent();

        InitializePlaybackTimer();
        
        // 监听ViewModel属性变化
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void InitializePlaybackTimer()
    {
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _playbackTimer.Tick += (s, e) =>
        {
            ViewModel.UpdatePlaybackTime();
            UpdateLyricsHighlight();
        };
    }

    #region Value Converters (用于x:Bind函数绑定)
    
    public string GetPlayPauseGlyph(bool isPlaying) => isPlaying ? "\uE769" : "\uE768";
    
    public int GetLyricFormatIndex(LyricFormat format) => (int)format;
    
    #endregion

    #region ViewModel Event Handlers

    private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.ParsedLyrics):
                UpdateLyricsPreview();
                break;
            case nameof(ViewModel.CurrentLyricIndex):
                UpdateLyricsHighlight();
                break;
        }
    }

    #endregion

    #region File Operations

    private async void NewPackage_Click(object sender, RoutedEventArgs e)
    {
        if (!await PromptSaveChanges()) return;

        try
        {
            ViewModel.CreateNewPackageCommand.Execute(null);
            ShowInfoBar("新建成功", "已创建新的音乐包", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfoBar("创建失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void OpenPackage_Click(object sender, RoutedEventArgs e)
    {
        if (!await PromptSaveChanges()) return;

        var file = await PickFileAsync(new[] { ".dmusicpak" }, PickerLocationId.DocumentsLibrary);
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
            await SavePackageAs();
        else
            await SavePackage(ViewModel.CurrentFilePath);
    }

    private async void SavePackageAs_Click(object sender, RoutedEventArgs e)
    {
        await SavePackageAs();
    }

    private async Task SavePackageAs()
    {
        var file = await SaveFileAsync(
            new[] { (".dmusicpak", new[] { ".dmusicpak" }) },
            string.IsNullOrEmpty(ViewModel.Title) ? "音乐包" : ViewModel.Title);
        
        if (file != null)
            await SavePackage(file.Path);
    }

    private async Task SavePackage(string path)
    {
        try
        {
            ViewModel.SavePackageCommand.Execute(path);
            ShowInfoBar("保存成功", $"已保存", InfoBarSeverity.Success);
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
            SavePackage_Click(null, null);
            return true;
        }

        return result == ContentDialogResult.Secondary;
    }

    #endregion

    #region Audio Operations

    private async void ImportAudio_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickFileAsync(new[] { ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".aac" }, PickerLocationId.MusicLibrary);
        if (file != null)
        {
            await ImportAudioFile(file);
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
            var file = items.OfType<StorageFile>().FirstOrDefault();
            
            if (file != null && IsAudioFile(file.FileType))
            {
                await ImportAudioFile(file);
            }
            else
            {
                ShowInfoBar("格式错误", "请拖拽支持的音频文件", InfoBarSeverity.Warning);
            }
        }
    }

    private async Task ImportAudioFile(StorageFile file)
    {
        try
        {
            await ViewModel.ImportAudioAsync(file);
            ShowInfoBar("导入成功", $"已导入: {file.Name}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    #endregion

    #region Cover Operations

    private async void ImportCover_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickFileAsync(new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" }, PickerLocationId.PicturesLibrary);
        if (file != null)
        {
            await ImportCoverFile(file);
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
            var file = items.OfType<StorageFile>().FirstOrDefault();
            
            if (file != null && IsImageFile(file.FileType))
            {
                await ImportCoverFile(file);
            }
            else
            {
                ShowInfoBar("格式错误", "请拖拽支持的图片文件", InfoBarSeverity.Warning);
            }
        }
    }

    private async Task ImportCoverFile(StorageFile file)
    {
        try
        {
            await ViewModel.ImportCoverAsync(file);
            ShowInfoBar("导入成功", "已导入封面", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfoBar("导入失败", ex.Message, InfoBarSeverity.Error);
        }
    }

    private void RemoveCover_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveCoverCommand.Execute(null);
        ShowInfoBar("已移除", "已移除封面", InfoBarSeverity.Informational);
    }

    #endregion

    #region Lyrics Operations

    private async void ImportLyrics_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickFileAsync(new[] { ".lrc", ".srt", ".ass" }, PickerLocationId.MusicLibrary);
        if (file != null)
        {
            try
            {
                string content = await FileIO.ReadTextAsync(file);
                ViewModel.LyricsText = content;

                // 自动识别格式
                ViewModel.LyricsFormat = file.FileType.ToLower() switch
                {
                    ".lrc" => LyricFormat.LrcLineByLine,
                    ".srt" => LyricFormat.SRT,
                    ".ass" => LyricFormat.ASS,
                    _ => LyricFormat.None
                };

                ShowInfoBar("导入成功", $"已导入: {file.Name}", InfoBarSeverity.Success);
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
        if (sender is ComboBox combo && ViewModel != null)
        {
            ViewModel.LyricsFormat = (LyricFormat)combo.SelectedIndex;
            ViewModel.ParseLyricsCommand.Execute(null);
        }
    }

    private void UpdateLyricsPreview()
    {
        if (LyricsPreviewPanel == null) return;

        LyricsPreviewPanel.Children.Clear();

        if (ViewModel.ParsedLyrics == null || ViewModel.ParsedLyrics.Count == 0)
        {
            LyricsPreviewPanel.Children.Add(new TextBlock
            {
                Text = "无歌词",
                Foreground = new SolidColorBrush(Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 150, 0, 0),
                FontSize = 16
            });
            return;
        }

        foreach (var lyric in ViewModel.ParsedLyrics)
        {
            var textBlock = new TextBlock
            {
                Text = lyric.Text,
                Foreground = new SolidColorBrush(Colors.Gray),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 4)
            };
            LyricsPreviewPanel.Children.Add(textBlock);
        }
    }

    private void UpdateLyricsHighlight()
    {
        if (LyricsPreviewPanel == null || ViewModel.ParsedLyrics == null) return;

        for (int i = 0; i < ViewModel.ParsedLyrics.Count && i < LyricsPreviewPanel.Children.Count; i++)
        {
            if (LyricsPreviewPanel.Children[i] is TextBlock textBlock)
            {
                if (i == ViewModel.CurrentLyricIndex)
                {
                    textBlock.Foreground = new SolidColorBrush(Colors.White);
                    textBlock.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
                    textBlock.FontSize = 17;
                    ScrollToLyric(textBlock);
                    
                    // 更新时间显示
                    if (CurrentLyricTimeText != null && ViewModel.ParsedLyrics[i].Time != null)
                    {
                        CurrentLyricTimeText.Text = ViewModel.ParsedLyrics[i].Text;
                    }
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

    private void ScrollToLyric(TextBlock textBlock)
    {
        try
        {
            if (LyricsScrollViewer == null) return;

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
            _playbackTimer.Start();
        else
            _playbackTimer.Stop();
    }

    #endregion

    #region Helpers

    private void ShowInfoBar(string title, string message, InfoBarSeverity severity)
    {
        if (InfoBar == null) return;

        InfoBar.Title = title;
        InfoBar.Message = message;
        InfoBar.Severity = severity;
        InfoBar.IsOpen = true;
    }

    private async Task<StorageFile> PickFileAsync(string[] extensions, PickerLocationId location)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.SuggestedStartLocation = location;
        foreach (var ext in extensions)
            picker.FileTypeFilter.Add(ext);
        return await picker.PickSingleFileAsync();
    }

    private async Task<StorageFile> SaveFileAsync((string name, string[] extensions)[] fileTypes, string suggestedFileName)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = suggestedFileName;
        foreach (var (name, extensions) in fileTypes)
            picker.FileTypeChoices.Add(name, extensions.ToList());
        return await picker.PickSaveFileAsync();
    }

    private bool IsAudioFile(string extension)
    {
        var audioExtensions = new[] { ".mp3", ".flac", ".wav", ".ogg", ".m4a", ".aac" };
        return audioExtensions.Contains(extension.ToLower());
    }

    private bool IsImageFile(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
        return imageExtensions.Contains(extension.ToLower());
    }

    #endregion
    
}