namespace DesktopMascot.Agent.Providers;

public static class TtsAudioFileValidator
{
    private const long MinimumAudioBytes = 512;

    public static TtsAudioFileValidationResult Validate(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Invalid("音频文件异常：路径为空。");

        if (!File.Exists(filePath))
            return Invalid($"音频文件异常：文件不存在：{filePath}");

        var info = new FileInfo(filePath);
        if (info.Length < MinimumAudioBytes)
            return Invalid($"音频文件异常：文件过小（{info.Length} 字节），可能生成失败。");

        var extension = Path.GetExtension(filePath);
        if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase) &&
            !HasMp3Header(filePath))
        {
            return Invalid("音频文件异常：不是有效的 MP3 文件。");
        }

        if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase) &&
            !HasWavHeader(filePath))
        {
            return Invalid("音频文件异常：不是有效的 WAV 文件。");
        }

        return new TtsAudioFileValidationResult(true, null);
    }

    private static TtsAudioFileValidationResult Invalid(string error) =>
        new(false, error);

    private static bool HasMp3Header(string filePath)
    {
        Span<byte> header = stackalloc byte[4];
        var bytesRead = ReadHeader(filePath, header);
        if (bytesRead < 3)
            return false;

        var hasId3Header = header[0] == 0x49 && header[1] == 0x44 && header[2] == 0x33;
        var hasFrameSync = bytesRead >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0;
        return hasId3Header || hasFrameSync;
    }

    private static bool HasWavHeader(string filePath)
    {
        Span<byte> header = stackalloc byte[12];
        var bytesRead = ReadHeader(filePath, header);
        if (bytesRead < 12)
            return false;

        return header[0] == 0x52 &&
               header[1] == 0x49 &&
               header[2] == 0x46 &&
               header[3] == 0x46 &&
               header[8] == 0x57 &&
               header[9] == 0x41 &&
               header[10] == 0x56 &&
               header[11] == 0x45;
    }

    private static int ReadHeader(string filePath, Span<byte> buffer)
    {
        using var stream = File.OpenRead(filePath);
        return stream.Read(buffer);
    }
}

public sealed record TtsAudioFileValidationResult(bool IsValid, string? Error);
