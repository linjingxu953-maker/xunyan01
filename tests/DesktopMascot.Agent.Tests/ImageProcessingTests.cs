using DesktopMascot.Agent.Tools;

namespace DesktopMascot.Agent.Tests;

public class ImageProcessingTests
{
    [Fact]
    public async Task Convert_ShouldConvertFormat()
    {
        var tool = new ImageProcessingTool();
        var tempDir = Path.GetTempPath();
        var inputFile = Path.Combine(tempDir, $"img_test_{Guid.NewGuid():N}.png");
        var outputFile = Path.Combine(tempDir, $"img_test_{Guid.NewGuid():N}.jpg");

        try
        {
            // 创建测试图像
            using var bmp = new System.Drawing.Bitmap(100, 100);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.Red);
            bmp.Save(inputFile, System.Drawing.Imaging.ImageFormat.Png);

            var args = JsonSerializer.Serialize(new
            {
                action = "convert",
                input_path = inputFile,
                output_path = outputFile,
                format = "jpg"
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("格式转换完成", result.Content);
            Assert.True(File.Exists(outputFile));
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task Compress_ShouldReduceSize()
    {
        var tool = new ImageProcessingTool();
        var tempDir = Path.GetTempPath();
        var inputFile = Path.Combine(tempDir, $"img_compress_{Guid.NewGuid():N}.png");
        var outputFile = Path.Combine(tempDir, $"img_compress_{Guid.NewGuid():N}.jpg");

        try
        {
            using var bmp = new System.Drawing.Bitmap(200, 200);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.Blue);
            bmp.Save(inputFile, System.Drawing.Imaging.ImageFormat.Png);

            var args = JsonSerializer.Serialize(new
            {
                action = "compress",
                input_path = inputFile,
                output_path = outputFile,
                quality = 50
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("压缩完成", result.Content);
            Assert.True(File.Exists(outputFile));
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task Resize_ShouldChangeDimensions()
    {
        var tool = new ImageProcessingTool();
        var tempDir = Path.GetTempPath();
        var inputFile = Path.Combine(tempDir, $"img_resize_{Guid.NewGuid():N}.png");
        var outputFile = Path.Combine(tempDir, $"img_resize_{Guid.NewGuid():N}.png");

        try
        {
            using var bmp = new System.Drawing.Bitmap(200, 200);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.Green);
            bmp.Save(inputFile, System.Drawing.Imaging.ImageFormat.Png);

            var args = JsonSerializer.Serialize(new
            {
                action = "resize",
                input_path = inputFile,
                output_path = outputFile,
                width = 100
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("缩放完成", result.Content);
            Assert.Contains("100", result.Content);
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task Crop_ShouldCropImage()
    {
        var tool = new ImageProcessingTool();
        var tempDir = Path.GetTempPath();
        var inputFile = Path.Combine(tempDir, $"img_crop_{Guid.NewGuid():N}.png");
        var outputFile = Path.Combine(tempDir, $"img_crop_{Guid.NewGuid():N}.png");

        try
        {
            using var bmp = new System.Drawing.Bitmap(200, 200);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.Yellow);
            bmp.Save(inputFile, System.Drawing.Imaging.ImageFormat.Png);

            var args = JsonSerializer.Serialize(new
            {
                action = "crop",
                input_path = inputFile,
                output_path = outputFile,
                x = 50,
                y = 50,
                width = 100,
                height = 100
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("裁剪完成", result.Content);
            Assert.True(File.Exists(outputFile));
        }
        finally
        {
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task Info_ShouldReturnImageInfo()
    {
        var tool = new ImageProcessingTool();
        var tempDir = Path.GetTempPath();
        var testFile = Path.Combine(tempDir, $"img_info_{Guid.NewGuid():N}.png");

        try
        {
            using var bmp = new System.Drawing.Bitmap(150, 100);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.White);
            bmp.Save(testFile, System.Drawing.Imaging.ImageFormat.Png);

            var args = JsonSerializer.Serialize(new
            {
                action = "info",
                input_path = testFile
            });

            var result = await tool.ExecuteAsync(args);

            Assert.True(result.Success);
            Assert.Contains("图像信息", result.Content);
            Assert.Contains("150x100", result.Content);
        }
        finally
        {
            if (File.Exists(testFile)) File.Delete(testFile);
        }
    }

    [Fact]
    public void ImageProcessingTool_Metadata_ShouldBeCorrect()
    {
        var tool = new ImageProcessingTool();
        Assert.Equal("image_processing", tool.Name);
        Assert.Contains("convert", tool.ParametersSchema);
        Assert.Contains("compress", tool.ParametersSchema);
        Assert.Contains("resize", tool.ParametersSchema);
    }
}
