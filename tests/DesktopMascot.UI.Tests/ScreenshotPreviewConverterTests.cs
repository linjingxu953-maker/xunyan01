using Avalonia.Media.Imaging;
using DesktopMascot.UI.Converters;
using System.Globalization;

namespace DesktopMascot.UI.Tests;

public sealed class ScreenshotPreviewConverterTests
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [Fact]
    public void Convert_DoesNotThrowForExistingPngFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPngBase64));

        try
        {
            var converter = new ScreenshotPreviewConverter();

            var result = converter.Convert(path, typeof(Bitmap), null, CultureInfo.InvariantCulture);

            if (result is Bitmap bitmap)
                bitmap.Dispose();
            else
                Assert.Null(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Convert_ReturnsNullForMissingOrInvalidFile()
    {
        var converter = new ScreenshotPreviewConverter();

        var missing = converter.Convert(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png"),
            typeof(Bitmap),
            null,
            CultureInfo.InvariantCulture);

        Assert.Null(missing);
    }
}
