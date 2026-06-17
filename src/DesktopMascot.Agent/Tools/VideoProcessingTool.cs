using DesktopMascot.Core.Tools;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 视频处理工具 - 剪辑/格式转换/视频转音频/短视频制作
/// 基于 FFmpeg 命令行调用
/// </summary>
public class VideoProcessingTool : ITool
{
    public string Name => "video_processing";
    public string Description => "视频处理：剪辑、格式转换、视频转音频、添加字幕、合并视频、提取音频。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["trim", "convert", "extract_audio", "add_subtitle", "merge", "info", "screenshot", "gif"], "description": "操作类型" },
            "input_path": { "type": "string", "description": "输入文件路径" },
            "output_path": { "type": "string", "description": "输出文件路径" },
            "start_time": { "type": "string", "description": "开始时间（HH:mm:ss 或 秒数）" },
            "end_time": { "type": "string", "description": "结束时间（HH:mm:ss 或 秒数）" },
            "duration": { "type": "string", "description": "持续时间（HH:mm:ss 或 秒数）" },
            "format": { "type": "string", "enum": ["mp4", "avi", "mov", "mkv", "webm", "mp3", "wav", "aac", "gif"], "description": "目标格式" },
            "bitrate": { "type": "string", "description": "视频比特率（如 1M, 2M）" },
            "resolution": { "type": "string", "description": "分辨率（如 1920x1080, 720p, 480p）" },
            "subtitle_path": { "type": "string", "description": "字幕文件路径（SRT）" },
            "subtitle_text": { "type": "string", "description": "内嵌字幕文本" },
            "input_paths": { "type": "array", "description": "合并时的输入文件列表" },
            "time_point": { "type": "string", "description": "截图时间点" },
            "fps": { "type": "integer", "description": "GIF帧率" },
            "width": { "type": "integer", "description": "输出宽度" }
        },
        "required": ["action"]
    }
    """;

    public bool RequiresConfirmation => true;
    public string ConfirmationMessage => "视频处理需要调用 FFmpeg，请确认系统已安装。";

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            if (!await CheckFfmpegAsync())
                return Fail("未找到 FFmpeg，请先安装：https://ffmpeg.org/download.html");

            return action switch
            {
                "trim" => await TrimVideoAsync(root, ct),
                "convert" => await ConvertFormatAsync(root, ct),
                "extract_audio" => await ExtractAudioAsync(root, ct),
                "add_subtitle" => await AddSubtitleAsync(root, ct),
                "merge" => await MergeVideosAsync(root, ct),
                "info" => await GetVideoInfoAsync(root, ct),
                "screenshot" => await TakeScreenshotAsync(root, ct),
                "gif" => await CreateGifAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"视频处理失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> TrimVideoAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var startTime = root.TryGetProperty("start_time", out var sEl) ? sEl.GetString() ?? "0" : "0";
        var duration = root.TryGetProperty("duration", out var dEl) ? dEl.GetString() : null;
        var endTime = root.TryGetProperty("end_time", out var eEl) ? eEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            $"trimmed_{Path.GetFileNameWithoutExtension(inputPath)}.mp4");

        var args = new StringBuilder();
        args.Append($"-y -ss {FormatTime(startTime)} -i \"{inputPath}\"");
        if (endTime != null)
            args.Append($" -to {FormatTime(endTime)}");
        else if (duration != null)
            args.Append($" -t {FormatTime(duration)}");
        args.Append($" -c copy \"{outputPath}\"");

        await RunFfmpegAsync(args.ToString(), ct);

        var info = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("视频剪辑完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"时间范围：{startTime} → {endTime ?? (duration != null ? $"开始+{duration}" : "末尾")}");
        sb.AppendLine($"文件大小：{info.Length / 1024.0 / 1024.0:F1} MB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ConvertFormatAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var format = root.TryGetProperty("format", out var fEl) ? fEl.GetString() ?? "mp4" : "mp4";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var bitrate = root.TryGetProperty("bitrate", out var bEl) ? bEl.GetString() : null;
        var resolution = root.TryGetProperty("resolution", out var rEl) ? rEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(inputPath)}.{format}");

        var args = new StringBuilder();
        args.Append($"-y -i \"{inputPath}\"");

        if (resolution != null)
        {
            var res = ParseResolution(resolution);
            args.Append($" -vf scale={res.Width}:{res.Height}");
        }

        if (bitrate != null)
            args.Append($" -b:v {bitrate}");

        args.Append($" \"{outputPath}\"");

        await RunFfmpegAsync(args.ToString(), ct);

        var info = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("格式转换完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"格式：{format.ToUpper()}");
        sb.AppendLine($"文件大小：{info.Length / 1024.0 / 1024.0:F1} MB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ExtractAudioAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var format = root.TryGetProperty("format", out var fEl) ? fEl.GetString() ?? "mp3" : "mp3";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(inputPath)}.{format}");

        var codec = format.ToLower() switch
        {
            "mp3" => "-codec:a libmp3lame -q:a 2",
            "wav" => "-codec:a pcm_s16le",
            "aac" => "-codec:a aac -b:a 192k",
            "flac" => "-codec:a flac",
            _ => "-codec:a copy"
        };

        var args = $"-y -i \"{inputPath}\" -vn {codec} \"{outputPath}\"";
        await RunFfmpegAsync(args, ct);

        var info = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("音频提取完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"格式：{format.ToUpper()}");
        sb.AppendLine($"文件大小：{info.Length / 1024.0 / 1024.0:F1} MB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AddSubtitleAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var subtitlePath = root.TryGetProperty("subtitle_path", out var spEl) ? spEl.GetString() : null;
        var subtitleText = root.TryGetProperty("subtitle_text", out var stEl) ? stEl.GetString() : null;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");
        if (subtitlePath == null && subtitleText == null)
            return Fail("需要 subtitle_path 或 subtitle_text");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            $"subtitled_{Path.GetFileNameWithoutExtension(inputPath)}.mp4");

        string vf;
        if (subtitlePath != null)
        {
            var subEscaped = subtitlePath.Replace("\\", "/").Replace(":", "\\:");
            vf = $"subtitles='{subEscaped}'";
        }
        else
        {
            var textEscaped = (subtitleText ?? "").Replace("'", "\\'").Replace(":", "\\:");
            vf = $"drawtext=text='{textEscaped}':fontsize=24:fontcolor=white:borderw=2:bordercolor=black:x=(w-text_w)/2:y=h-th-20";
        }

        var args = $"-y -i \"{inputPath}\" -vf \"{vf}\" -c:a copy \"{outputPath}\"";
        await RunFfmpegAsync(args, ct);

        var sb = new StringBuilder();
        sb.AppendLine("字幕添加完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"字幕：{subtitlePath ?? "内嵌文本"}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> MergeVideosAsync(JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("input_paths", out var pathsEl) || pathsEl.GetArrayLength() == 0)
            return Fail("缺少 input_paths 参数");

        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var paths = pathsEl.EnumerateArray().Select(p => p.GetString() ?? "").ToList();

        outputPath ??= Path.Combine(Path.GetDirectoryName(paths[0]) ?? ".",
            $"merged_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        var listFile = Path.GetTempFileName() + ".txt";
        try
        {
            var content = string.Join("\n", paths.Select(p => $"file '{p.Replace("\\", "/")}'"));
            await File.WriteAllTextAsync(listFile, content);

            var args = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"";
            await RunFfmpegAsync(args, ct);

            var info = new FileInfo(outputPath);
            var sb = new StringBuilder();
            sb.AppendLine("视频合并完成");
            sb.AppendLine($"输入数量：{paths.Count}");
            foreach (var p in paths)
                sb.AppendLine($"  - {Path.GetFileName(p)}");
            sb.AppendLine($"输出：{outputPath}");
            sb.AppendLine($"文件大小：{info.Length / 1024.0 / 1024.0:F1} MB");

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        finally
        {
            if (File.Exists(listFile)) File.Delete(listFile);
        }
    }

    private async Task<ToolResult> GetVideoInfoAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        var args = $"-v quiet -print_format json -show_format -show_streams \"{inputPath}\"";
        var output = await RunFfprobeRawAsync(args, ct);

        var doc = JsonDocument.Parse(output);
        var format = doc.RootElement.TryGetProperty("format", out var fEl) ? fEl : default;
        var streams = doc.RootElement.TryGetProperty("streams", out var sEl) ? sEl : default;

        var sb = new StringBuilder();
        sb.AppendLine("视频信息");
        sb.AppendLine($"文件：{Path.GetFileName(inputPath)}");

        if (format.ValueKind != JsonValueKind.Undefined)
        {
            if (format.TryGetProperty("duration", out var durEl))
            {
                var durVal = double.Parse(durEl.GetString() ?? "0");
                sb.AppendLine($"时长：{TimeSpan.FromSeconds(durVal):hh\\:mm\\:ss}");
            }
            if (format.TryGetProperty("size", out var sizeEl))
            {
                var sizeVal = long.Parse(sizeEl.GetString() ?? "0");
                sb.AppendLine($"大小：{sizeVal / 1024.0 / 1024.0:F1} MB");
            }
            if (format.TryGetProperty("bit_rate", out var brEl))
            {
                var brVal = long.Parse(brEl.GetString() ?? "0");
                sb.AppendLine($"比特率：{brVal / 1000} kbps");
            }
        }

        if (streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var ctEl) ? ctEl.GetString() : "";
                if (codecType == "video")
                {
                    var cnVal = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() : "unknown";
                    var wVal = stream.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                    var hVal = stream.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                    var frVal = stream.TryGetProperty("r_frame_rate", out var fr) ? fr.GetString() : "unknown";
                    sb.AppendLine($"视频编码：{cnVal}");
                    sb.AppendLine($"分辨率：{wVal}x{hVal}");
                    sb.AppendLine($"帧率：{frVal}");
                }
                else if (codecType == "audio")
                {
                    var cn2Val = stream.TryGetProperty("codec_name", out var cn2) ? cn2.GetString() : "unknown";
                    var srVal = stream.TryGetProperty("sample_rate", out var sr) ? sr.GetString() : "unknown";
                    sb.AppendLine($"音频编码：{cn2Val}");
                    sb.AppendLine($"采样率：{srVal}");
                }
            }
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> TakeScreenshotAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var timePoint = root.TryGetProperty("time_point", out var tEl) ? tEl.GetString() ?? "00:00:01" : "00:00:01";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            $"screenshot_{Path.GetFileNameWithoutExtension(inputPath)}.jpg");

        var args = $"-y -ss {timePoint} -i \"{inputPath}\" -vframes 1 -q:v 2 \"{outputPath}\"";
        await RunFfmpegAsync(args, ct);

        var sb = new StringBuilder();
        sb.AppendLine("视频截图完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"时间点：{timePoint}");
        sb.AppendLine($"输出：{outputPath}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> CreateGifAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = GetRequiredString(root, "input_path");
        var startTime = root.TryGetProperty("start_time", out var sEl) ? sEl.GetString() ?? "0" : "0";
        var duration = root.TryGetProperty("duration", out var dEl) ? dEl.GetString() ?? "3" : "3";
        var fps = root.TryGetProperty("fps", out var fEl) ? fEl.GetInt32() : 10;
        var width = root.TryGetProperty("width", out var wEl) ? wEl.GetInt32() : 480;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path");
        if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(inputPath)}.gif");

        var args = $"-y -ss {FormatTime(startTime)} -t {duration} -i \"{inputPath}\" " +
                   $"-vf \"fps={fps},scale={width}:-1:flags=lanczos,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse\" " +
                   $"\"{outputPath}\"";

        await RunFfmpegAsync(args, ct);

        var info = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("GIF 创建完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"时长：{duration}秒，帧率：{fps}fps，宽度：{width}px");
        sb.AppendLine($"文件大小：{info.Length / 1024.0:F1} KB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static async Task<bool> CheckFfmpegAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Task<string> RunFfmpegRawAsync(string arguments, CancellationToken ct)
    {
        return RunProcessRawAsync("ffmpeg", "FFmpeg", arguments, ct);
    }

    private static Task<string> RunFfprobeRawAsync(string arguments, CancellationToken ct)
    {
        return RunProcessRawAsync("ffprobe", "FFprobe", arguments, ct);
    }

    private static async Task<string> RunProcessRawAsync(
        string fileName,
        string displayName,
        string arguments,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"无法启动 {displayName}");
        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{displayName} 错误：{error}");

        return output;
    }

    private static async Task RunFfmpegAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 FFmpeg");
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg 错误：{error}");
    }

    private static string FormatTime(string time)
    {
        if (double.TryParse(time, out var seconds))
            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss\.fff");
        return time;
    }

    private static (int Width, int Height) ParseResolution(string resolution)
    {
        if (resolution.Contains('x'))
        {
            var parts = resolution.Split('x');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        return resolution.ToLower() switch
        {
            "4k" or "2160p" => (3840, 2160),
            "1080p" or "fhd" => (1920, 1080),
            "720p" or "hd" => (1280, 720),
            "480p" or "sd" => (854, 480),
            "360p" => (640, 360),
            _ => (1280, 720)
        };
    }

    private static string? GetRequiredString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) ? el.GetString() : null;
    }

    private static ToolResult Fail(string error) => new() { Name = "video_processing", Success = false, Error = error };
}
