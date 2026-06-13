using Avalonia.Controls;

namespace DesktopMascot.UI.Services;

internal static class TrayIconFactory
{
    public static WindowIcon CreateDefaultIcon()
    {
        return new WindowIcon(new MemoryStream(CreateIconBytes()));
    }

    private static byte[] CreateIconBytes()
    {
        const int width = 16;
        const int height = 16;
        const int pixelBytes = width * height * 4;
        const int maskBytes = height * 4;
        const int bitmapHeaderBytes = 40;
        const int imageBytes = bitmapHeaderBytes + pixelBytes + maskBytes;
        const int imageOffset = 22;

        var buffer = new byte[imageOffset + imageBytes];
        var writer = new BinaryWriter(new MemoryStream(buffer));

        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)width);
        writer.Write((byte)height);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)imageBytes);
        writer.Write((uint)imageOffset);

        writer.Write((uint)bitmapHeaderBytes);
        writer.Write((int)width);
        writer.Write((int)(height * 2));
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)0);
        writer.Write((uint)pixelBytes);
        writer.Write((int)0);
        writer.Write((int)0);
        writer.Write((uint)0);
        writer.Write((uint)0);

        for (var y = height - 1; y >= 0; y--)
        {
            for (var x = 0; x < width; x++)
            {
                var dx = x - 7.5;
                var dy = y - 7.5;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var inside = distance <= 7.4;
                var edge = distance is > 6.2 and <= 7.4;

                byte alpha = inside ? (byte)255 : (byte)0;
                byte red = edge ? (byte)37 : (byte)255;
                byte green = edge ? (byte)99 : (byte)255;
                byte blue = edge ? (byte)235 : (byte)255;

                if (inside && !edge)
                {
                    red = 238;
                    green = 246;
                    blue = 255;
                }

                if (x is >= 5 and <= 10 && y is >= 5 and <= 10)
                {
                    red = 37;
                    green = 99;
                    blue = 235;
                }

                writer.Write(blue);
                writer.Write(green);
                writer.Write(red);
                writer.Write(alpha);
            }
        }

        writer.Write(new byte[maskBytes]);
        writer.Flush();

        return buffer;
    }
}
