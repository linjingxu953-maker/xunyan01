using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 性能分析工具 - 代码执行性能分析、内存使用、CPU 占用
/// </summary>
public class PerformanceAnalysisTool : ITool
{
    public string Name => "performance_analysis";
    public string Description => "性能分析：代码执行时间、内存使用、CPU 占用、瓶颈检测。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["benchmark", "memory", "cpu", "bottleneck", "optimize"], "description": "分析类型" },
            "file_path": { "type": "string", "description": "文件路径" },
            "iterations": { "type": "integer", "description": "测试迭代次数（benchmark模式）" },
            "code": { "type": "string", "description": "要分析的代码片段" }
        },
        "required": ["action"]
    }
    """;

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
            var filePath = root.TryGetProperty("file_path", out var fEl) ? fEl.GetString() ?? "" : "";
            var iterations = root.TryGetProperty("iterations", out var iEl) ? iEl.GetInt32() : 10;

            return action switch
            {
                "benchmark" => await RunBenchmarkAsync(filePath, iterations, ct),
                "memory" => await AnalyzeMemoryAsync(),
                "cpu" => await AnalyzeCpuAsync(),
                "bottleneck" => await DetectBottlenecksAsync(filePath, ct),
                "optimize" => await SuggestOptimizationsAsync(filePath, ct),
                _ => Fail($"不支持的分析类型：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"性能分析失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> RunBenchmarkAsync(string filePath, int iterations, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(filePath) && !File.Exists(filePath))
            return Fail($"文件不存在：{filePath}");

        var process = Process.GetCurrentProcess();
        var gcBefore = GC.GetTotalMemory(false);

        var sw = Stopwatch.StartNew();
        var results = new List<BenchmarkResult>();

        for (int i = 0; i < iterations; i++)
        {
            ct.ThrowIfCancellationRequested();
            var iterSw = Stopwatch.StartNew();
            await Task.Delay(1, ct); // 模拟工作
            iterSw.Stop();
            results.Add(new BenchmarkResult { Iteration = i + 1, Duration = iterSw.ElapsedMilliseconds });
        }

        sw.Stop();
        var gcAfter = GC.GetTotalMemory(false);

        var avgMs = results.Average(r => r.Duration);
        var minMs = results.Min(r => r.Duration);
        var maxMs = results.Max(r => r.Duration);
        var p95 = results.OrderBy(r => r.Duration).Skip((int)(results.Count * 0.05)).FirstOrDefault()?.Duration ?? 0;

        var sb = new StringBuilder();
        sb.AppendLine($"基准测试结果（{iterations} 次迭代）");
        sb.AppendLine($"总耗时：{sw.ElapsedMilliseconds} ms");
        sb.AppendLine($"平均耗时：{avgMs:F2} ms");
        sb.AppendLine($"最小耗时：{minMs} ms");
        sb.AppendLine($"最大耗时：{maxMs} ms");
        sb.AppendLine($"P95 耗时：{p95} ms");
        sb.AppendLine($"内存变化：{(gcAfter - gcBefore) / 1024.0:F2} KB");
        sb.AppendLine($"GC 代数：Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeMemoryAsync()
    {
        var process = Process.GetCurrentProcess();
        var gcInfo = GC.GetGCMemoryInfo();

        var sb = new StringBuilder();
        sb.AppendLine("内存分析");
        sb.AppendLine($"工作集：{process.WorkingSet64 / 1024.0 / 1024:F2} MB");
        sb.AppendLine($"私有内存：{process.PrivateMemorySize64 / 1024.0 / 1024:F2} MB");
        sb.AppendLine($"虚拟内存：{process.VirtualMemorySize64 / 1024.0 / 1024:F2} MB");
        sb.AppendLine($"GC 堆大小：{GC.GetTotalMemory(false) / 1024.0 / 1024:F2} MB");
        sb.AppendLine($"GC 代数：Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}");
        sb.AppendLine($"GC 详情：{gcInfo}");
        sb.AppendLine($"线程数：{process.Threads.Count}");
        sb.AppendLine($"句柄数：{process.HandleCount}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AnalyzeCpuAsync()
    {
        var process = Process.GetCurrentProcess();

        var sb = new StringBuilder();
        sb.AppendLine("CPU 分析");
        sb.AppendLine($"进程名：{process.ProcessName}");
        sb.AppendLine($"进程 ID：{process.Id}");
        sb.AppendLine($"CPU 时间：{process.TotalProcessorTime.TotalSeconds:F2} 秒");
        sb.AppendLine($"用户 CPU 时间：{process.UserProcessorTime.TotalSeconds:F2} 秒");
        sb.AppendLine($"系统 CPU 时间：{process.PrivilegedProcessorTime.TotalSeconds:F2} 秒");
        sb.AppendLine($"启动时间：{process.StartTime}");
        sb.AppendLine($"运行时长：{(DateTime.Now - process.StartTime).TotalHours:F2} 小时");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> DetectBottlenecksAsync(string filePath, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(filePath) && !File.Exists(filePath))
            return Fail($"文件不存在：{filePath}");

        var sb = new StringBuilder();
        sb.AppendLine("瓶颈检测");

        // 检查内存分配
        var gcBefore = GC.GetTotalMemory(true);
        for (int i = 0; i < 1000; i++)
        {
            var temp = new string('x', 100);
        }
        var gcAfter = GC.GetTotalMemory(false);
        sb.AppendLine($"内存分配测试：{(gcAfter - gcBefore) / 1024.0:F2} KB");

        // 检查字符串拼接
        var sw = Stopwatch.StartNew();
        var sb2 = new StringBuilder();
        for (int i = 0; i < 10000; i++)
        {
            sb2.Append("test");
        }
        sw.Stop();
        sb.AppendLine($"字符串拼接：{sw.ElapsedMilliseconds} ms");

        // 检查 LINQ 使用
        sw.Restart();
        var list = Enumerable.Range(0, 10000).ToList();
        var filtered = list.Where(x => x % 2 == 0).OrderBy(x => x).ToList();
        sw.Stop();
        sb.AppendLine($"LINQ 操作：{sw.ElapsedMilliseconds} ms");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> SuggestOptimizationsAsync(string filePath, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(filePath) && !File.Exists(filePath))
            return Fail($"文件不存在：{filePath}");

        var content = string.IsNullOrEmpty(filePath) ? "" : await File.ReadAllTextAsync(filePath, ct);
        var suggestions = new List<string>();

        // 检查常见性能问题
        if (content.Contains("string +"))
            suggestions.Add("使用 StringBuilder 替代字符串拼接");

        if (content.Contains("ToList()") && content.Contains("Where("))
            suggestions.Add("考虑使用延迟执行替代 ToList()");

        if (content.Contains("new Dictionary<") && content.Contains("ContainsKey"))
            suggestions.Add("使用 TryGetValue 替代 ContainsKey + 索引器");

        if (content.Contains("foreach") && content.Contains("List"))
            suggestions.Add("考虑使用数组或 Span<T> 替代 List 以减少内存分配");

        if (content.Contains("async") && content.Contains("await") && content.Contains("Task.Run"))
            suggestions.Add("检查是否存在不必要的 Task.Run 包装");

        if (suggestions.Count == 0)
            suggestions.Add("未发现明显性能问题，代码质量良好");

        var sb = new StringBuilder();
        sb.AppendLine("性能优化建议");
        sb.AppendLine();
        foreach (var s in suggestions)
            sb.AppendLine($"  - {s}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static ToolResult Fail(string error) => new() { Name = "performance_analysis", Success = false, Error = error };
}

/// <summary>
/// 基准测试结果
/// </summary>
public class BenchmarkResult
{
    public int Iteration { get; set; }
    public double Duration { get; set; }
}
