using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 图像处理工具 - 格式转换、压缩、调整大小
/// </summary>
public class ImageProcessingTool : ITool
{
    public string Name => "image_processing";
    public string Description => "图像处理：格式转换、压缩、调整大小、裁剪、水印。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["convert", "compress", "resize", "crop", "thumbnail", "info"], "description": "操作类型" },
            "input_path": { "type": "string", "description": "输入图像路径" },
            "output_path": { "type": "string", "description": "输出图像路径" },
            "format": { "type": "string", "enum": ["png", "jpg", "jpeg", "bmp", "gif", "webp"], "description": "目标格式" },
            "quality": { "type": "integer", "description": "压缩质量（1-100）" },
            "width": { "type": "integer", "description": "目标宽度" },
            "height": { "type": "integer", "description": "目标高度" },
            "max_size_kb": { "type": "integer", "description": "最大文件大小（KB）" }
        },
        "required": ["action", "input_path"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
            var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(inputPath)) return Fail("缺少 input_path 参数");
            if (!File.Exists(inputPath)) return Fail($"文件不存在：{inputPath}");

            return action switch
            {
                "convert" => await ConvertFormatAsync(root, ct),
                "compress" => await CompressImageAsync(root, ct),
                "resize" => await ResizeImageAsync(root, ct),
                "crop" => await CropImageAsync(root, ct),
                "thumbnail" => await CreateThumbnailAsync(root, ct),
                "info" => GetImageInfo(root),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"图像处理失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> ConvertFormatAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var format = root.TryGetProperty("format", out var fEl) ? fEl.GetString() ?? "png" : "png";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        outputPath ??= Path.ChangeExtension(inputPath, format);

        using var image = Image.FromFile(inputPath);

        if (format.ToLower() is "jpg" or "jpeg")
        {
            var encoder = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
            image.Save(outputPath, encoder, encoderParams);
        }
        else
        {
            var imageFormat = format.ToLower() switch
            {
                "bmp" => ImageFormat.Bmp,
                "gif" => ImageFormat.Gif,
                _ => ImageFormat.Png
            };
            image.Save(outputPath, imageFormat);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("格式转换完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"目标格式：{format.ToUpper()}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> CompressImageAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var quality = root.TryGetProperty("quality", out var qEl) ? qEl.GetInt32() : 75;
        var maxSizeKb = root.TryGetProperty("max_size_kb", out var sEl) ? sEl.GetInt32() : 0;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        outputPath ??= Path.ChangeExtension(inputPath, "jpg");
        quality = Math.Clamp(quality, 1, 100);

        using var image = Image.FromFile(inputPath);

        var encoder = ImageCodecInfo.GetImageEncoders()
            .First(e => e.FormatID == ImageFormat.Jpeg.Guid);

        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

        image.Save(outputPath, encoder, encoderParams);

        var fileInfo = new FileInfo(outputPath);
        var sizeKb = fileInfo.Length / 1024.0;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("图像压缩完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"质量：{quality}%");
        sb.AppendLine($"文件大小：{sizeKb:F1} KB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ResizeImageAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var width = root.TryGetProperty("width", out var wEl) ? wEl.GetInt32() : 0;
        var height = root.TryGetProperty("height", out var hEl) ? hEl.GetInt32() : 0;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (width <= 0 && height <= 0) return Fail("需要指定 width 或 height");

        outputPath ??= Path.ChangeExtension(inputPath, Path.GetExtension(inputPath));

        using var image = Image.FromFile(inputPath);

        if (width <= 0) width = (int)(image.Width * ((double)height / image.Height));
        if (height <= 0) height = (int)(image.Height * ((double)width / image.Width));

        using var resized = new Bitmap(width, height);
        using var g = Graphics.FromImage(resized);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(image, 0, 0, width, height);

        resized.Save(outputPath);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("图像缩放完成");
        sb.AppendLine($"输入：{inputPath} ({image.Width}x{image.Height})");
        sb.AppendLine($"输出：{outputPath} ({width}x{height})");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> CropImageAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var x = root.TryGetProperty("x", out var xEl) ? xEl.GetInt32() : 0;
        var y = root.TryGetProperty("y", out var yEl) ? yEl.GetInt32() : 0;
        var width = root.TryGetProperty("width", out var wEl) ? wEl.GetInt32() : 100;
        var height = root.TryGetProperty("height", out var hEl) ? hEl.GetInt32() : 100;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        outputPath ??= Path.ChangeExtension(inputPath, Path.GetExtension(inputPath));

        using var image = Image.FromFile(inputPath);
        var rect = new Rectangle(x, y, width, height);
        using var cropped = new Bitmap(width, height, image.PixelFormat);
        using (var g = Graphics.FromImage(cropped))
        {
            g.DrawImage(image, new Rectangle(0, 0, width, height), rect, GraphicsUnit.Pixel);
        }
        cropped.Save(outputPath);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("图像裁剪完成");
        sb.AppendLine($"输入：{inputPath}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"裁剪区域：({x}, {y}) {width}x{height}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> CreateThumbnailAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var maxSize = root.TryGetProperty("max_size_kb", out var sEl) ? sEl.GetInt32() : 100;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        outputPath ??= Path.ChangeExtension(inputPath, "thumb.jpg");

        using var image = Image.FromFile(inputPath);
        var ratio = Math.Min(200.0 / image.Width, 200.0 / image.Height);
        var thumbWidth = (int)(image.Width * ratio);
        var thumbHeight = (int)(image.Height * ratio);

        using var thumb = new Bitmap(thumbWidth, thumbHeight);
        using var g = Graphics.FromImage(thumb);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(image, 0, 0, thumbWidth, thumbHeight);

        var encoder = ImageCodecInfo.GetImageEncoders().First(e => e.FormatID == ImageFormat.Jpeg.Guid);
        var encoderParams = new EncoderParameters(1);
        encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 85L);
        thumb.Save(outputPath, encoder, encoderParams);

        var fileInfo = new FileInfo(outputPath);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("缩略图生成完成");
        sb.AppendLine($"输入：{inputPath} ({image.Width}x{image.Height})");
        sb.AppendLine($"输出：{outputPath} ({thumbWidth}x{thumbHeight})");
        sb.AppendLine($"文件大小：{fileInfo.Length / 1024.0:F1} KB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult GetImageInfo(JsonElement root)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";

        using var image = Image.FromFile(inputPath);
        var fileInfo = new FileInfo(inputPath);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("图像信息");
        sb.AppendLine($"文件：{inputPath}");
        sb.AppendLine($"格式：{ImageFormatToName(image.RawFormat)}");
        sb.AppendLine($"尺寸：{image.Width}x{image.Height}");
        sb.AppendLine($"像素格式：{image.PixelFormat}");
        sb.AppendLine($"文件大小：{fileInfo.Length / 1024.0:F1} KB");
        sb.AppendLine($"分辨率：{image.HorizontalResolution:F0} DPI");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static string ImageFormatToName(ImageFormat format)
    {
        if (format == ImageFormat.Jpeg) return "JPEG";
        if (format == ImageFormat.Png) return "PNG";
        if (format == ImageFormat.Bmp) return "BMP";
        if (format == ImageFormat.Gif) return "GIF";
        if (format == ImageFormat.Tiff) return "TIFF";
        return "Unknown";
    }

    private static ToolResult Fail(string error) => new() { Name = "image_processing", Success = false, Error = error };
}
