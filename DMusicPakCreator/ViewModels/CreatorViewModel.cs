using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DMusicPakCreator.Services;
using DMusicPakDotNet;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;

namespace DMusicPakCreator.ViewModels;

public partial class CreatorViewModel : ObservableObject, IDisposable
{
    private readonly PackageService _packageService;
    private readonly AudioService _audioService;
    private readonly CoverService _coverService;
    private readonly LyricsService _lyricsService;
    private readonly MediaPlayerService _mediaPlayerService;

    // 状态
    [ObservableProperty] private bool _isModified;
    [ObservableProperty] private string _currentFilePath;
    [ObservableProperty] private string _statusText = "就绪";
    [ObservableProperty] private string _windowTitle = "DMusicPak Creator";

    // 音频
    [ObservableProperty] private byte[] _audioData;
    [ObservableProperty] private string _audioFileName;
    [ObservableProperty] private string _audioFileSize;
    [ObservableProperty] private bool _hasAudio;

    // 元数据
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _artist;
    [ObservableProperty] private string _album;
    [ObservableProperty] private string _genre;
    [ObservableProperty] private string _year;
    [ObservableProperty] private string _comment;
    [ObservableProperty] private string _duration;
    [ObservableProperty] private string _bitrate;
    [ObservableProperty] private string _sampleRate;
    [ObservableProperty] private string _channels;

    // 封面
    [ObservableProperty] private byte[] _coverData;
    [ObservableProperty] private BitmapImage _coverImage;
    [ObservableProperty] private string _coverInfo;
    [ObservableProperty] private bool _hasCover;
    private CoverFormat _coverFormat;
    private uint _coverWidth;
    private uint _coverHeight;

    // 歌词
    [ObservableProperty] private string _lyricsText;
    [ObservableProperty] private LyricFormat _lyricsFormat;
    [ObservableProperty] private List<LyricLine> _parsedLyrics = new();
    [ObservableProperty] private int _currentLyricIndex = -1;

    // 播放
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _canPlay;
    [ObservableProperty] private string _playbackTime = "00:00 / 00:00";

    public CreatorViewModel(
        PackageService packageService,
        AudioService audioService,
        CoverService coverService,
        LyricsService lyricsService,
        MediaPlayerService mediaPlayerService)
    {
        _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        _coverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
        _lyricsService = lyricsService ?? throw new ArgumentNullException(nameof(lyricsService));
        _mediaPlayerService = mediaPlayerService ?? throw new ArgumentNullException(nameof(mediaPlayerService));

        // 订阅播放器事件
        _mediaPlayerService.MediaOpened += OnMediaOpened;
        _mediaPlayerService.MediaEnded += OnMediaEnded;

        Debug.WriteLine("CreatorViewModel - 已初始化");
    }

    #region Package Operations

    [RelayCommand]
    private void CreateNewPackage()
    {
        _packageService.CreateNew();
        ClearForm();
        IsModified = false;
        CurrentFilePath = null;
        UpdateWindowTitle();
        StatusText = "新建音乐包";
    }

    [RelayCommand]
    private async Task LoadPackageAsync(string filePath)
    {
        try
        {
            StatusText = "正在加载...";
            _packageService.Load(filePath);
            CurrentFilePath = filePath;

            await LoadPackageDataAsync();

            IsModified = false;
            UpdateWindowTitle();
            StatusText = $"已加载: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载包失败: {ex.Message}");
            StatusText = "加载失败";
            throw;
        }
    }

    [RelayCommand]
    private void SavePackage(string filePath)
    {
        try
        {
            StatusText = "正在保存...";
            
            // 保存所有数据到包
            SaveToPackage();
            
            // 保存包到文件
            _packageService.Save(filePath);
            
            CurrentFilePath = filePath;
            IsModified = false;
            UpdateWindowTitle();
            StatusText = $"已保存: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存包失败: {ex.Message}");
            StatusText = "保存失败";
            throw;
        }
    }

    private async Task LoadPackageDataAsync()
    {
        // 加载元数据
        var metadata = _packageService.GetMetadata();
        if (metadata != null)
        {
            Title = metadata.Title ?? string.Empty;
            Artist = metadata.Artist ?? string.Empty;
            Album = metadata.Album ?? string.Empty;
            Genre = metadata.Genre ?? string.Empty;
            Year = metadata.Year ?? string.Empty;
            Comment = metadata.Comment ?? string.Empty;
            Duration = metadata.DurationMs.ToString();
            Bitrate = metadata.Bitrate.ToString();
            SampleRate = metadata.SampleRate.ToString();
            Channels = metadata.Channels.ToString();
        }

        // 加载音频
        var audio = _packageService.GetAudio();
        if (audio?.Data != null && audio.Data.Length > 0)
        {
            AudioData = audio.Data;
            AudioFileName = audio.SourceFilename ?? "audio.mp3";
            AudioFileSize = _audioService.FormatFileSize(audio.Data.Length);
            HasAudio = true;

            // 加载用于播放
            await _mediaPlayerService.LoadFromDataAsync(AudioData, AudioFileName);
            CanPlay = true;
        }

        // 加载歌词
        var lyrics = _packageService.GetLyrics();
        if (lyrics?.Data != null && lyrics.Data.Length > 0)
        {
            LyricsText = lyrics.Text ?? string.Empty;
            LyricsFormat = lyrics.Format;
            ParseLyrics();
        }

        // 加载封面
        var cover = _packageService.GetCover();
        if (cover?.Data != null && cover.Data.Length > 0)
        {
            CoverData = cover.Data;
            _coverFormat = cover.Format;
            _coverWidth = cover.Width;
            _coverHeight = cover.Height;
            
            CoverImage = await _coverService.CreateBitmapFromCoverAsync(cover);
            CoverInfo = _coverService.FormatCoverInfo(cover, cover.Data.Length);
            HasCover = true;
        }
    }

    private void SaveToPackage()
    {
        // 保存元数据
        var metadata = new Metadata
        {
            Title = Title ?? string.Empty,
            Artist = Artist ?? string.Empty,
            Album = Album ?? string.Empty,
            Genre = Genre ?? string.Empty,
            Year = Year ?? string.Empty,
            Comment = Comment ?? string.Empty
        };

        if (uint.TryParse(Duration, out uint duration))
            metadata.DurationMs = duration;
        if (uint.TryParse(Bitrate, out uint bitrate))
            metadata.Bitrate = bitrate;
        if (uint.TryParse(SampleRate, out uint sampleRate))
            metadata.SampleRate = sampleRate;
        if (ushort.TryParse(Channels, out ushort channels))
            metadata.Channels = channels;

        _packageService.SetMetadata(metadata);

        // 保存音频
        if (AudioData != null && AudioData.Length > 0)
        {
            var audio = new Audio
            {
                SourceFilename = AudioFileName,
                Data = AudioData
            };
            _packageService.SetAudio(audio);
        }

        // 保存封面
        if (CoverData != null && CoverData.Length > 0)
        {
            var cover = new Cover
            {
                Format = _coverFormat,
                Data = CoverData,
                Width = _coverWidth,
                Height = _coverHeight
            };
            _packageService.SetCover(cover);
        }

        // 保存歌词
        if (!string.IsNullOrWhiteSpace(LyricsText))
        {
            var lyrics = _lyricsService.CreateLyrics(LyricsText, LyricsFormat);
            if (lyrics != null)
            {
                _packageService.SetLyrics(lyrics);
            }
        }
    }

    #endregion

    #region Audio Operations

    public async Task ImportAudioAsync(Windows.Storage.StorageFile file)
    {
        try
        {
            StatusText = "正在导入音频...";

            var (data, fileName) = await _audioService.ImportAudioFromFileAsync(file);
            AudioData = data;
            AudioFileName = fileName;
            AudioFileSize = _audioService.FormatFileSize(data.Length);
            HasAudio = true;

            // 自动填充元数据
            var metadata = await _audioService.ExtractMetadataAsync(file);
            if (!string.IsNullOrEmpty(metadata.Title) && string.IsNullOrEmpty(Title))
                Title = metadata.Title;
            if (!string.IsNullOrEmpty(metadata.Artist) && string.IsNullOrEmpty(Artist))
                Artist = metadata.Artist;
            if (!string.IsNullOrEmpty(metadata.Album) && string.IsNullOrEmpty(Album))
                Album = metadata.Album;
            if (!string.IsNullOrEmpty(metadata.Year) && string.IsNullOrEmpty(Year))
                Year = metadata.Year;
            if (metadata.DurationMs > 0)
                Duration = metadata.DurationMs.ToString();
            if (metadata.Bitrate > 0)
                Bitrate = metadata.Bitrate.ToString();

            // 加载用于播放
            await _mediaPlayerService.LoadFromFileAsync(file);
            CanPlay = true;

            IsModified = true;
            StatusText = $"已导入: {fileName}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导入音频失败: {ex.Message}");
            StatusText = "导入失败";
            throw;
        }
    }

    #endregion

    #region Cover Operations

    public async Task ImportCoverAsync(Windows.Storage.StorageFile file)
    {
        try
        {
            StatusText = "正在导入封面...";

            var cover = await _coverService.ImportCoverFromFileAsync(file);
            CoverData = cover.Data;
            _coverFormat = cover.Format;
            _coverWidth = cover.Width;
            _coverHeight = cover.Height;

            CoverImage = await _coverService.CreateBitmapFromFileAsync(file);
            CoverInfo = _coverService.FormatCoverInfo(cover, cover.Data.Length);
            HasCover = true;

            IsModified = true;
            StatusText = "已导入封面";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导入封面失败: {ex.Message}");
            StatusText = "导入失败";
            throw;
        }
    }

    [RelayCommand]
    private void RemoveCover()
    {
        CoverData = null;
        CoverImage = null;
        CoverInfo = string.Empty;
        HasCover = false;
        IsModified = true;
    }

    #endregion

    #region Lyrics Operations

    [RelayCommand]
    private void ParseLyrics()
    {
        if (string.IsNullOrWhiteSpace(LyricsText))
        {
            ParsedLyrics = new List<LyricLine>();
            return;
        }

        if (LyricsFormat >= LyricFormat.LrcESLyric && LyricsFormat <= LyricFormat.LrcLineByLine)
        {
            ParsedLyrics = _lyricsService.ParseLrcLyrics(LyricsText);
        }
    }

    [RelayCommand]
    private void ClearLyrics()
    {
        LyricsText = string.Empty;
        LyricsFormat = LyricFormat.None;
        ParsedLyrics = new List<LyricLine>();
    }

    public void UpdateCurrentLyric(TimeSpan currentTime)
    {
        if (ParsedLyrics == null || ParsedLyrics.Count == 0)
            return;

        var newIndex = _lyricsService.FindCurrentLyricIndex(ParsedLyrics, currentTime);
        if (newIndex != CurrentLyricIndex)
        {
            CurrentLyricIndex = newIndex;
        }
    }

    #endregion

    #region Playback Operations

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (!CanPlay) return;

        if (IsPlaying)
        {
            _mediaPlayerService.Pause();
            IsPlaying = false;
        }
        else
        {
            _mediaPlayerService.Play();
            IsPlaying = true;
        }
    }

    public void UpdatePlaybackTime()
    {
        if (!CanPlay) return;

        var current = _mediaPlayerService.Position;
        var total = _mediaPlayerService.Duration;
        PlaybackTime = $"{_lyricsService.FormatTime(current)} / {_lyricsService.FormatTime(total)}";

        // 更新歌词
        UpdateCurrentLyric(current);
    }

    private void OnMediaOpened(object sender, object args)
    {
        var duration = _mediaPlayerService.Duration;
        PlaybackTime = $"00:00 / {_lyricsService.FormatTime(duration)}";
    }

    private void OnMediaEnded(object sender, object args)
    {
        IsPlaying = false;
        CurrentLyricIndex = -1;
    }

    #endregion

    #region Helpers

    private void ClearForm()
    {
        Title = string.Empty;
        Artist = string.Empty;
        Album = string.Empty;
        Genre = string.Empty;
        Year = string.Empty;
        Comment = string.Empty;
        Duration = string.Empty;
        Bitrate = string.Empty;
        SampleRate = string.Empty;
        Channels = string.Empty;

        AudioData = null;
        AudioFileName = null;
        AudioFileSize = null;
        HasAudio = false;

        CoverData = null;
        CoverImage = null;
        CoverInfo = string.Empty;
        HasCover = false;

        LyricsText = string.Empty;
        LyricsFormat = LyricFormat.None;
        ParsedLyrics = new List<LyricLine>();

        CanPlay = false;
        IsPlaying = false;
    }

    private void UpdateWindowTitle()
    {
        var title = "DMusicPak Creator";
        if (!string.IsNullOrEmpty(CurrentFilePath))
        {
            title += $" - {Path.GetFileName(CurrentFilePath)}";
        }
        else if (_packageService.HasPackage)
        {
            title += " - 未命名";
        }

        if (IsModified)
        {
            title += " *";
        }

        WindowTitle = title;
    }

    partial void OnIsModifiedChanged(bool value)
    {
        UpdateWindowTitle();
    }

    partial void OnLyricsTextChanged(string value)
    {
        IsModified = true;
        ParseLyrics();
    }

    #endregion

    public void Dispose()
    {
        _mediaPlayerService?.Dispose();
        _packageService?.Dispose();
        Debug.WriteLine("CreatorViewModel - 已释放");
    }
}