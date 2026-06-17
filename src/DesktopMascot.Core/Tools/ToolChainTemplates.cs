namespace DesktopMascot.Core.Tools;

/// <summary>
/// 工具链模板 — 预定义的多工具工作流
/// </summary>
public static class ToolChainTemplates
{
    /// <summary>
    /// 短视频制作流程：脚本生成 → 素材合成 → 配音 → 输出
    /// </summary>
    public static ToolChain ShortVideoProduction(string title, string voiceText, string outputPath)
    {
        return new ToolChainBuilder("短视频制作")
            .WithDescription($"制作短视频：{title}")
            .WithVariable("title", title)
            .WithVariable("voice_text", voiceText)
            .WithVariable("output_path", outputPath)
            .AddStep("short_video_maker", """{"action":"generate_script","title":"{title}","duration":30}""")
            .AddStep("short_video_maker", """{"action":"generate_composition","title":"{title}"}""")
            .AddStep("short_video_maker", """{"action":"render_composition","composition_dir":"{composition_dir}"}""")
            .AddStep("short_video_maker", """{"action":"add_voiceover","input_path":"{rendered_path}","voice_text":"{voice_text}","output_path":"{output_path}"}""")
            .Build();
    }

    /// <summary>
    /// 文件安全扫描 + 修复流程
    /// </summary>
    public static ToolChain SecurityScanAndFix(string directory)
    {
        return new ToolChainBuilder("安全扫描与修复")
            .WithDescription($"扫描目录安全问题：{directory}")
            .WithVariable("directory", directory)
            .AddStep("security_scan", """{"action":"vulnerabilities","path":"{directory}"}""")
            .AddStep("code_analysis", """{"action":"quality","path":"{directory}"}""")
            .Build();
    }

    /// <summary>
    /// 图像批量处理流程
    /// </summary>
    public static ToolChain ImageBatchProcess(string inputDir, string outputDir, string format, int quality = 80)
    {
        return new ToolChainBuilder("图像批量处理")
            .WithDescription($"批量处理图像：{inputDir} → {outputDir}")
            .WithVariable("input_dir", inputDir)
            .WithVariable("output_dir", outputDir)
            .WithVariable("format", format)
            .WithVariable("quality", quality.ToString())
            .AddStep("batch_file_processor", """{"action":"list","directory":"{input_dir}","extension":".png,.jpg,.jpeg,.bmp"}""")
            .AddStep("image_processing", """{"action":"compress","input_path":"{input_dir}","output_path":"{output_dir}","quality":{quality}}""")
            .Build();
    }

    /// <summary>
    /// 视频转 GIF 流程
    /// </summary>
    public static ToolChain VideoToGif(string inputPath, string outputPath, int duration = 5, int fps = 10)
    {
        return new ToolChainBuilder("视频转GIF")
            .WithDescription($"将视频转换为 GIF：{inputPath}")
            .WithVariable("input_path", inputPath)
            .WithVariable("output_path", outputPath)
            .AddStep("video_processing", """{"action":"info","input_path":"{input_path}"}""")
            .AddStep("video_processing", """{"action":"gif","input_path":"{input_path}","output_path":"{output_path}","duration":5,"fps":10}""")
            .Build();
    }

    /// <summary>
    /// 项目健康检查流程
    /// </summary>
    public static ToolChain ProjectHealthCheck(string projectPath)
    {
        return new ToolChainBuilder("项目健康检查")
            .WithDescription($"检查项目健康状况：{projectPath}")
            .WithVariable("project_path", projectPath)
            .AddStep("list_directory", """{"path":"{project_path}"}""")
            .AddStep("code_analysis", """{"action":"quality","path":"{project_path}"}""")
            .AddStep("code_analysis", """{"action":"complexity","path":"{project_path}"}""")
            .AddStep("security_scan", """{"action":"secrets","path":"{project_path}"}""")
            .Build();
    }

    /// <summary>
    /// 文件备份流程
    /// </summary>
    public static ToolChain BackupFiles(string sourceDir, string backupDir)
    {
        return new ToolChainBuilder("文件备份")
            .WithDescription($"备份文件：{sourceDir} → {backupDir}")
            .WithVariable("source_dir", sourceDir)
            .WithVariable("backup_dir", backupDir)
            .AddStep("batch_file_processor", """{"action":"copy","source":"{source_dir}","destination":"{backup_dir}","recursive":"true"}""")
            .AddStep("cloud_sync", """{"action":"upload","local_path":"{backup_dir}","remote_path":"backups/"}""")
            .Build();
    }

    /// <summary>
    /// 网页内容分析流程
    /// </summary>
    public static ToolChain WebContentAnalysis(string url)
    {
        return new ToolChainBuilder("网页内容分析")
            .WithDescription($"分析网页内容：{url}")
            .WithVariable("url", url)
            .AddStep("network_request", """{"method":"GET","url":"{url}"}""")
            .AddStep("code_analysis", """{"action":"stats","path":"{url}"}""")
            .Build();
    }

    /// <summary>
    /// 数据库备份 + 清理流程
    /// </summary>
    public static ToolChain DatabaseMaintenance(string dbPath)
    {
        return new ToolChainBuilder("数据库维护")
            .WithDescription($"维护数据库：{dbPath}")
            .WithVariable("db_path", dbPath)
            .AddStep("database", """{"action":"connect","path":"{db_path}"}""")
            .AddStep("database", """{"action":"stats","path":"{db_path}"}""")
            .AddStep("database", """{"action":"query","path":"{db_path}","sql":"VACUUM"}""")
            .Build();
    }
}
