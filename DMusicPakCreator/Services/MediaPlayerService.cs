using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using System.Diagnostics;

namespace DMusicPakCreator.Services;

public class MediaPlayerService : IDisposable
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly AudioService _audioService;
    private bool _disposed;

    public event EventHandler<object> MediaOpened;
    public event EventHandler<object> MediaEnded;
    public event EventHandler<MediaPlayerFailedEventArgs> MediaFailed;

    public bool IsPlaying { get; private set; }
    public TimeSpan Position => _mediaPlayer?.PlaybackSession?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => _mediaPlayer?.NaturalDuration ?? TimeSpan.Zero;

    public MediaPlayerService(AudioService audioService)
    {
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        
        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.MediaOpened += OnMediaOpened;
        _mediaPlayer.MediaEnded += OnMediaEnded;
        _mediaPlayer.MediaFailed += OnMediaFailed;

        Debug.WriteLine("MediaPlayerService - 已初始化");
    }

    /// <summary>
    /// 从音频数据加载媒体
    /// </summary>
    public async Task LoadFromDataAsync(byte[] audioData, string fileName)
    {
        if (audioData == null || audioData.Length == 0)
            throw new ArgumentException("音频数据为空", nameof(audioData));

        Debug.WriteLine($"MediaPlayerService - 加载音频数据: {fileName}");

        // 创建临时文件
        var tempFile = await _audioService.CreateTempAudioFileAsync(audioData, fileName);

        // 打开文件流
        var stream = await tempFile.OpenReadAsync();
        
        // 获取 Content Type
        var contentType = _audioService.GetContentType(fileName);
        
        // 创建 MediaSource
        var mediaSource = MediaSource.CreateFromStream(stream, contentType);
        
        // 设置到 MediaPlayer
        _mediaPlayer.Source = mediaSource;

        Debug.WriteLine("MediaPlayerService - 媒体已加载");
    }

    /// <summary>
    /// 从文件加载媒体
    /// </summary>
    public async Task LoadFromFileAsync(StorageFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        Debug.WriteLine($"MediaPlayerService - 加载文件: {file.Name}");

        var stream = await file.OpenReadAsync();
        var contentType = _audioService.GetContentType(file.Name);
        var mediaSource = MediaSource.CreateFromStream(stream, contentType);
        
        _mediaPlayer.Source = mediaSource;

        Debug.WriteLine("MediaPlayerService - 媒体已加载");
    }

    /// <summary>
    /// 播放
    /// </summary>
    public void Play()
    {
        if (_mediaPlayer?.PlaybackSession == null)
        {
            Debug.WriteLine("MediaPlayerService - PlaybackSession 未就绪");
            return;
        }

        _mediaPlayer.Play();
        IsPlaying = true;
        Debug.WriteLine("MediaPlayerService - 开始播放");
    }

    /// <summary>
    /// 暂停
    /// </summary>
    public void Pause()
    {
        _mediaPlayer?.Pause();
        IsPlaying = false;
        Debug.WriteLine("MediaPlayerService - 已暂停");
    }

    /// <summary>
    /// 停止
    /// </summary>
    public void Stop()
    {
        _mediaPlayer?.Pause();
        if (_mediaPlayer?.PlaybackSession != null)
        {
            _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
        }
        IsPlaying = false;
        Debug.WriteLine("MediaPlayerService - 已停止");
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        Debug.WriteLine($"MediaPlayerService - 媒体已打开, 时长: {sender.NaturalDuration}");
        MediaOpened?.Invoke(sender, args);
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        IsPlaying = false;
        Debug.WriteLine("MediaPlayerService - 播放结束");
        MediaEnded?.Invoke(sender, args);
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        IsPlaying = false;
        Debug.WriteLine($"MediaPlayerService - 播放失败: {args.ErrorMessage}");
        MediaFailed?.Invoke(sender, args);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _mediaPlayer?.Dispose();
            _disposed = true;
            Debug.WriteLine("MediaPlayerService - 已释放");
        }
    }
}