using DMusicPakDotNet;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DMusicPakCreator.Services;

public class LyricLine
{
    public TimeSpan Time { get; set; }
    public string Text { get; set; }
}

public class LyricsService
{
    /// <summary>
    /// 解析LRC格式歌词
    /// </summary>
    public List<LyricLine> ParseLrcLyrics(string lrcText)
    {
        var lyrics = new List<LyricLine>();

        if (string.IsNullOrEmpty(lrcText))
        {
            Debug.WriteLine("LyricsService - 歌词文本为空");
            return lyrics;
        }

        try
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
                    lyrics.Add(new LyricLine
                    {
                        Time = time,
                        Text = text
                    });
                }
            }

            // 按时间排序
            lyrics = lyrics.OrderBy(l => l.Time).ToList();
            Debug.WriteLine($"LyricsService - 解析了 {lyrics.Count} 行歌词");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LyricsService - 解析失败: {ex.Message}");
        }

        return lyrics;
    }

    /// <summary>
    /// 根据当前时间查找应该显示的歌词索引
    /// </summary>
    public int FindCurrentLyricIndex(List<LyricLine> lyrics, TimeSpan currentTime)
    {
        if (lyrics == null || lyrics.Count == 0)
            return -1;

        int index = -1;
        for (int i = 0; i < lyrics.Count; i++)
        {
            if (lyrics[i].Time <= currentTime)
            {
                index = i;
            }
            else
            {
                break;
            }
        }

        return index;
    }

    /// <summary>
    /// 创建 Lyrics 对象
    /// </summary>
    public Lyrics CreateLyrics(string text, LyricFormat format)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return new Lyrics
        {
            Format = format,
            Data = System.Text.Encoding.UTF8.GetBytes(text)
        };
    }

    /// <summary>
    /// 格式化时间显示
    /// </summary>
    public string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return time.ToString(@"h\:mm\:ss");
        return time.ToString(@"mm\:ss");
    }
}