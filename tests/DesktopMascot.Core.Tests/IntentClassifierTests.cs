using DesktopMascot.Core.Services;

namespace DesktopMascot.Core.Tests;

public class IntentClassifierTests
{
    [Fact]
    public void Classify_SummarizePage_ShouldDetect()
    {
        var result = IntentClassifier.Classify("帮我总结这个网页");
        Assert.Equal("summarize_page", result.Intent);
        Assert.True(result.Confidence >= 0.5f);
    }

    [Fact]
    public void Classify_AnalyzeError_ShouldDetect()
    {
        var result = IntentClassifier.Classify("这个报错是什么意思");
        Assert.Equal("analyze_error", result.Intent);
    }

    [Fact]
    public void Classify_InspectProject_ShouldDetect()
    {
        var result = IntentClassifier.Classify("帮我看看这个项目");
        Assert.Equal("inspect_project", result.Intent);
    }

    [Fact]
    public void Classify_WriteFile_ShouldDetect()
    {
        var result = IntentClassifier.Classify("帮我写入文件");
        Assert.Equal("write_file", result.Intent);
    }

    [Fact]
    public void Classify_RunCommand_ShouldDetect()
    {
        var result = IntentClassifier.Classify("执行命令 dir");
        Assert.Equal("run_command", result.Intent);
    }

    [Fact]
    public void Classify_Negation_ShouldSkip()
    {
        var result = IntentClassifier.Classify("不要总结网页");
        Assert.NotEqual("summarize_page", result.Intent);
    }

    [Fact]
    public void Classify_ScreenUnderstand_ShouldDetect()
    {
        var result = IntentClassifier.Classify("帮我圈选屏幕");
        Assert.Equal("screen_understand", result.Intent);
    }

    [Fact]
    public void Classify_VideoProcessing_ShouldDetect()
    {
        var result = IntentClassifier.Classify("剪视频");
        Assert.Equal("video_processing", result.Intent);
    }

    [Fact]
    public void Classify_SecurityScan_ShouldDetect()
    {
        var result = IntentClassifier.Classify("安全扫描");
        Assert.Equal("security_scan", result.Intent);
    }

    [Fact]
    public void Classify_ImageProcessing_ShouldDetect()
    {
        var result = IntentClassifier.Classify("压缩图片");
        Assert.Equal("image_processing", result.Intent);
    }

    [Fact]
    public void Classify_Database_ShouldDetect()
    {
        var result = IntentClassifier.Classify("数据库查询");
        Assert.Equal("database_operation", result.Intent);
    }

    [Fact]
    public void Classify_CloudSync_ShouldDetect()
    {
        var result = IntentClassifier.Classify("云同步文件");
        Assert.Equal("cloud_sync", result.Intent);
    }

    [Fact]
    public void Classify_Email_ShouldDetect()
    {
        var result = IntentClassifier.Classify("发邮件");
        Assert.Equal("email", result.Intent);
    }

    [Fact]
    public void Classify_UnknownInput_ShouldReturnChat()
    {
        var result = IntentClassifier.Classify("今天天气真好");
        Assert.Equal("chat", result.Intent);
    }

    [Fact]
    public void Classify_EmptyInput_ShouldReturnChat()
    {
        var result = IntentClassifier.Classify("");
        Assert.Equal("chat", result.Intent);
    }

    [Fact]
    public void Classify_ShouldExtractUrl()
    {
        var result = IntentClassifier.Classify("访问 https://example.com");
        Assert.True(result.Entities.ContainsKey("url"));
        Assert.Equal("https://example.com", result.Entities["url"]);
    }

    [Fact]
    public void Classify_ShouldExtractFilePath()
    {
        var result = IntentClassifier.Classify("读取 C:\\test\\file.cs");
        Assert.True(result.Entities.ContainsKey("file_path"));
    }

    [Fact]
    public void Classify_Memory_ShouldDetect()
    {
        var result = IntentClassifier.Classify("记住我喜欢深色主题");
        Assert.Equal("update_memory", result.Intent);
    }

    [Fact]
    public void Classify_NetworkRequest_ShouldDetect()
    {
        var result = IntentClassifier.Classify("发请求到 api");
        Assert.Equal("network_request", result.Intent);
    }

    [Fact]
    public void Classify_FileEncryption_ShouldDetect()
    {
        var result = IntentClassifier.Classify("加密文件");
        Assert.Equal("file_encryption", result.Intent);
    }

    [Fact]
    public void Classify_ShortVideo_ShouldDetect()
    {
        var result = IntentClassifier.Classify("做个短视频");
        Assert.Equal("short_video", result.Intent);
    }
}
