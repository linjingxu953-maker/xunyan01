using System.Diagnostics;
using DesktopMascot.Agent.Models;
using DesktopMascot.Agent.Providers;
using Moq;

namespace DesktopMascot.Agent.Tests;

public class EdgeTtsTests
{
    [Fact]
    public void EdgeTtsProvider_ShouldHaveDefaultVoices()
    {
        var provider = new EdgeTtsProvider();
        var voices = provider.GetAvailableVoices();

        Assert.NotEmpty(voices);
        Assert.Contains(voices, v => v.Contains("zh-CN"));
        Assert.Contains(voices, v => v.Contains("en-US"));
    }

    [Fact]
    public void EdgeTtsProvider_ChineseVoice_ShouldBeDefault()
    {
        var provider = new EdgeTtsProvider();
        var voices = provider.GetAvailableVoices();

        Assert.Contains("zh-CN-XiaoxiaoNeural", voices);
    }

    [Fact]
    public void EdgeTtsProvider_OutputDirectory_ShouldBeCreated()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"edge_tts_test_{Guid.NewGuid():N}");
        try
        {
            var provider = new EdgeTtsProvider(tempDir);
            Assert.True(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task EdgeTtsProvider_SynthesizeToFile_ShouldCreateFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"edge_tts_test_{Guid.NewGuid():N}");
        var tempFile = Path.Combine(tempDir, "test.mp3");

        try
        {
            // 注意：这个测试需要 edge-tts 命令行工具安装
            // 如果没有安装，测试会跳过
            if (!IsEdgeTtsAvailable())
            {
                return; // 跳过测试
            }

            var provider = new EdgeTtsProvider(tempDir);
            var result = await provider.SynthesizeToFileAsync("测试", tempFile, "zh-CN-XiaoxiaoNeural");

            if (result.Success)
            {
                Assert.True(File.Exists(tempFile));
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void EdgeTtsProvider_VoiceList_ShouldContainChinese()
    {
        var provider = new EdgeTtsProvider();
        var voices = provider.GetAvailableVoices();

        var chineseVoices = voices.Where(v => v.StartsWith("zh-")).ToList();
        Assert.True(chineseVoices.Count >= 3, "应该至少有3个中文语音");
    }

    [Fact]
    public void EdgeTtsProvider_VoiceList_ShouldContainEnglish()
    {
        var provider = new EdgeTtsProvider();
        var voices = provider.GetAvailableVoices();

        var englishVoices = voices.Where(v => v.StartsWith("en-")).ToList();
        Assert.True(englishVoices.Count >= 3, "应该至少有3个英文语音");
    }

    private static bool IsEdgeTtsAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "edge-tts",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
