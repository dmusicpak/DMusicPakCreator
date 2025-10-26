using DMusicPakDotNet;
using System.Diagnostics;

namespace DMusicPakCreator.Services;

public class PackageService : IDisposable
{
    private Package _currentPackage;
    private bool _disposed;

    public bool HasPackage => _currentPackage != null;

    /// <summary>
    /// 创建新包
    /// </summary>
    public void CreateNew()
    {
        Debug.WriteLine("PackageService - 创建新包");
        _currentPackage?.Dispose();
        _currentPackage = new Package();
    }

    /// <summary>
    /// 从文件加载包
    /// </summary>
    public void Load(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        Debug.WriteLine($"PackageService - 加载包: {filePath}");
        _currentPackage?.Dispose();
        _currentPackage = new Package(filePath);
    }

    /// <summary>
    /// 保存包到文件
    /// </summary>
    public void Save(string filePath)
    {
        if (_currentPackage == null)
            throw new InvalidOperationException("没有打开的包");

        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        Debug.WriteLine($"PackageService - 保存包: {filePath}");
        _currentPackage.Save(filePath);
    }

    /// <summary>
    /// 设置元数据
    /// </summary>
    public void SetMetadata(Metadata metadata)
    {
        if (_currentPackage == null)
            throw new InvalidOperationException("没有打开的包");

        _currentPackage.SetMetadata(metadata);
        Debug.WriteLine($"PackageService - 设置元数据: {metadata.Title}");
    }

    /// <summary>
    /// 获取元数据
    /// </summary>
    public Metadata GetMetadata()
    {
        if (_currentPackage == null)
            return new Metadata();

        return _currentPackage.GetMetadata();
    }

    /// <summary>
    /// 设置音频
    /// </summary>
    public void SetAudio(Audio audio)
    {
        if (_currentPackage == null)
            throw new InvalidOperationException("没有打开的包");

        _currentPackage.SetAudio(audio);
        Debug.WriteLine($"PackageService - 设置音频: {audio.SourceFilename}");
    }

    /// <summary>
    /// 获取音频
    /// </summary>
    public Audio GetAudio()
    {
        if (_currentPackage == null)
            return null;

        return _currentPackage.GetAudio();
    }

    /// <summary>
    /// 设置封面
    /// </summary>
    public void SetCover(Cover cover)
    {
        if (_currentPackage == null)
            throw new InvalidOperationException("没有打开的包");

        _currentPackage.SetCover(cover);
        Debug.WriteLine($"PackageService - 设置封面: {cover.Width}×{cover.Height}");
    }

    /// <summary>
    /// 获取封面
    /// </summary>
    public Cover GetCover()
    {
        if (_currentPackage == null)
            return null;

        return _currentPackage.GetCover();
    }

    /// <summary>
    /// 设置歌词
    /// </summary>
    public void SetLyrics(Lyrics lyrics)
    {
        if (_currentPackage == null)
            throw new InvalidOperationException("没有打开的包");

        _currentPackage.SetLyrics(lyrics);
        Debug.WriteLine($"PackageService - 设置歌词: {lyrics.Format}");
    }

    /// <summary>
    /// 获取歌词
    /// </summary>
    public Lyrics GetLyrics()
    {
        if (_currentPackage == null)
            return null;

        return _currentPackage.GetLyrics();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _currentPackage?.Dispose();
            _currentPackage = null;
            _disposed = true;
            Debug.WriteLine("PackageService - 已释放");
        }
    }
}