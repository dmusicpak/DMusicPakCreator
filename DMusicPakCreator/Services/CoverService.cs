using Windows.Storage;
using Windows.Storage.Streams;
using DMusicPakDotNet;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;

namespace DMusicPakCreator.Services;

public class CoverService
{
    /// <summary>
    /// 从文件导入封面数据
    /// </summary>
    public async Task<Cover> ImportCoverFromFileAsync(StorageFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        Debug.WriteLine($"CoverService - 导入封面: {file.Name}");

        var buffer = await FileIO.ReadBufferAsync(file);
        var imageData = new byte[buffer.Length];
        using (var reader = DataReader.FromBuffer(buffer))
        {
            reader.ReadBytes(imageData);
        }

        var format = DetectImageFormat(file.FileType);
        var props = await file.Properties.GetImagePropertiesAsync();

        var cover = new Cover
        {
            Format = format,
            Data = imageData,
            Width = props.Width,
            Height = props.Height
        };

        Debug.WriteLine($"CoverService - 封面: {cover.Width}×{cover.Height}, {cover.Format}");
        return cover;
    }

    /// <summary>
    /// 从封面数据创建 BitmapImage
    /// </summary>
    public async Task<BitmapImage> CreateBitmapFromCoverAsync(Cover cover)
    {
        if (cover?.Data == null || cover.Data.Length == 0)
            throw new ArgumentException("封面数据为空", nameof(cover));

        var bitmap = new BitmapImage();
        using (var ms = new MemoryStream(cover.Data))
        {
            var stream = ms.AsRandomAccessStream();
            await bitmap.SetSourceAsync(stream);
        }

        return bitmap;
    }

    /// <summary>
    /// 从文件创建 BitmapImage
    /// </summary>
    public async Task<BitmapImage> CreateBitmapFromFileAsync(StorageFile file)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        var bitmap = new BitmapImage();
        using (var stream = await file.OpenAsync(FileAccessMode.Read))
        {
            await bitmap.SetSourceAsync(stream);
        }

        return bitmap;
    }

    /// <summary>
    /// 检测图片格式
    /// </summary>
    public CoverFormat DetectImageFormat(string fileExtension)
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

    /// <summary>
    /// 格式化封面信息文本
    /// </summary>
    public string FormatCoverInfo(Cover cover, long fileSize)
    {
        if (cover == null)
            return string.Empty;

        var sizeText = FormatFileSize(fileSize);
        return $"{cover.Width}×{cover.Height} • {sizeText}";
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
}