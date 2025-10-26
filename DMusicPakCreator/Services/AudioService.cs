using Windows.Storage;
using Windows.Storage.Streams;
using DMusicPakDotNet;
using System.Diagnostics;

namespace DMusicPakCreator.Services;

public class AudioService
{
    private readonly StorageFolder _tempFolder;

    public AudioService()
    {
        // 尝试获取临时文件夹
        try
        {
            _tempFolder = ApplicationData.Current.TemporaryFolder;
            Debug.WriteLine($"✅ AudioService - 临时文件夹: {_tempFolder.Path}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ AudioService - 无法获取 ApplicationData: {ex.Message}");
            _tempFolder = null;
        }
    }

    /// <summary>
    /// 从文件导入音频数据
    /// </summary>
    public async Task<(byte[] data, string fileName)> ImportAudioFromFileAsync(StorageFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        Debug.WriteLine($"AudioService - 导入音频: {file.Name}");

        var buffer = await FileIO.ReadBufferAsync(file);
        var data = new byte[buffer.Length];
        using (var reader = DataReader.FromBuffer(buffer))
        {
            reader.ReadBytes(data);
        }

        Debug.WriteLine($"AudioService - 音频大小: {data.Length} 字节");
        return (data, file.Name);
    }

    /// <summary>
    /// 自动识别音频元数据
    /// </summary>
    public async Task<Metadata> ExtractMetadataAsync(StorageFile file)
    {
        var metadata = new Metadata();

        try
        {
            var props = await file.Properties.GetMusicPropertiesAsync();

            metadata.Title = props.Title ?? string.Empty;
            metadata.Artist = props.Artist ?? string.Empty;
            metadata.Album = props.Album ?? string.Empty;
            metadata.Year = props.Year > 0 ? props.Year.ToString() : string.Empty;

            if (props.Duration.TotalMilliseconds > 0)
                metadata.DurationMs = (uint)props.Duration.TotalMilliseconds;

            if (props.Bitrate > 0)
                metadata.Bitrate = props.Bitrate / 1000;

            Debug.WriteLine($"AudioService - 提取元数据: {metadata.Title} - {metadata.Artist}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioService - 提取元数据失败: {ex.Message}");
        }

        return metadata;
    }

    /// <summary>
    /// 创建用于播放的临时文件
    /// </summary>
    public async Task<StorageFile> CreateTempAudioFileAsync(byte[] audioData, string fileName)
    {
        if (audioData == null || audioData.Length == 0)
            throw new ArgumentException("音频数据为空", nameof(audioData));

        Debug.WriteLine($"AudioService - 创建临时文件: {fileName}");

        // 清理文件名
        fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

        StorageFile tempFile;

        if (_tempFolder != null)
        {
            // 方案A: 使用 ApplicationData
            Debug.WriteLine($"AudioService - 使用 ApplicationData 临时文件夹");
            tempFile = await _tempFolder.CreateFileAsync(
                fileName,
                CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBytesAsync(tempFile, audioData);
        }
        else
        {
            // 方案B: 使用系统临时文件夹
            Debug.WriteLine($"AudioService - 使用系统临时文件夹");
            var systemTempPath = Path.GetTempPath();
            var fullPath = Path.Combine(systemTempPath, fileName);
            await Task.Run(() => File.WriteAllBytes(fullPath, audioData));
            tempFile = await StorageFile.GetFileFromPathAsync(fullPath);
        }

        Debug.WriteLine($"AudioService - 临时文件已创建: {tempFile.Path}");
        return tempFile;
    }

    /// <summary>
    /// 获取音频文件的 Content Type
    /// </summary>
    public string GetContentType(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "audio/mpeg";

        var ext = Path.GetExtension(fileName).ToLower();
        return ext switch
        {
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            _ => "audio/mpeg"
        };
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    public string FormatFileSize(long bytes)
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
}