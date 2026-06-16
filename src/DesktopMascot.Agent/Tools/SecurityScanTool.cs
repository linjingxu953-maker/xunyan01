using System.Text;
using System.Text.RegularExpressions;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 安全扫描工具 - 代码安全漏洞检测、敏感信息扫描
/// </summary>
public class SecurityScanTool : ITool
{
    public string Name => "security_scan";
    public string Description => "安全扫描：检测代码安全漏洞、敏感信息泄露、权限问题。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["vulnerabilities", "secrets", "permissions", "full"], "description": "扫描类型" },
            "file_path": { "type": "string", "description": "文件路径" },
            "directory": { "type": "string", "description": "目录路径（full模式）" }
        },
        "required": ["action"]
    }
    """;

    private static readonly string[] SecretPatterns = new[]
    {
        @"api[_-]?key\s*[=:]\s*['""][^'""]+['""]",
        @"secret\s*[=:]\s*['""][^'""]+['""]",
        @"password\s*[=:]\s*['""][^'""]+['""]",
        @"token\s*[=:]\s*['""][^'""]+['""]",
        @"AKIA[0-9A-Z]{16}",  // AWS Access Key
        @"sk-[0-9a-zA-Z]{32,}",  // OpenAI API Key
        @"ghp_[0-9a-zA-Z]{36}",  // GitHub Token
    };

    private static readonly string[] VulnerabilityPatterns = new[]
    {
        (@"eval\s*\(", "潜在代码注入"),
        (@"exec\s*\(", "潜在命令注入"),
        (@"innerHTML\s*=", "XSS 风险"),
        (@"document\.write\s*\(", "XSS 风险"),
        (@"SELECT\s+.*FROM\s+.*WHERE\s+.*\+", "SQL 注入风险"),
        (@"\.Execute\s*\(", "SQL 注入风险"),
        (@"Process\.Start\s*\(.*true\)", "命令注入风险"),
    };

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
            var filePath = root.TryGetProperty("file_path", out var fEl) ? fEl.GetString() ?? "" : "";
            var directory = root.TryGetProperty("directory", out var dEl) ? dEl.GetString() ?? "" : "";

            return action switch
            {
                "vulnerabilities" => await ScanVulnerabilitiesAsync(filePath, ct),
                "secrets" => await ScanSecretsAsync(filePath, ct),
                "permissions" => await ScanPermissionsAsync(filePath, ct),
                "full" => await FullScanAsync(directory, ct),
                _ => Fail($"不支持的扫描类型：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"安全扫描失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> ScanVulnerabilitiesAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var findings = new List<SecurityFinding>();

        foreach (var (pattern, description) in VulnerabilityPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var lineNum = content[..m.Index].Count(c => c == '\n') + 1;
                findings.Add(new SecurityFinding
                {
                    Type = "vulnerability",
                    Severity = "medium",
                    Description = description,
                    File = filePath,
                    Line = lineNum,
                    Evidence = m.Value
                });
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"漏洞扫描结果：{filePath}");
        sb.AppendLine($"发现 {findings.Count} 个潜在问题");
        sb.AppendLine();

        foreach (var f in findings)
            sb.AppendLine($"  [{f.Severity}] 行 {f.Line}: {f.Description}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ScanSecretsAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var findings = new List<SecurityFinding>();

        foreach (var pattern in SecretPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var lineNum = content[..m.Index].Count(c => c == '\n') + 1;
                var masked = MaskSecret(m.Value);
                findings.Add(new SecurityFinding
                {
                    Type = "secret",
                    Severity = "high",
                    Description = "检测到敏感信息",
                    File = filePath,
                    Line = lineNum,
                    Evidence = masked
                });
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"敏感信息扫描结果：{filePath}");
        sb.AppendLine($"发现 {findings.Count} 个潜在泄露");
        sb.AppendLine();

        foreach (var f in findings)
            sb.AppendLine($"  [{f.Severity}] 行 {f.Line}: {f.Description} ({f.Evidence})");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ScanPermissionsAsync(string filePath, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"权限检查：{filePath}");

        if (File.Exists(filePath))
        {
            var info = new FileInfo(filePath);
            sb.AppendLine($"文件：{info.FullName}");
            sb.AppendLine($"大小：{info.Length} 字节");
            sb.AppendLine($"创建时间：{info.CreationTime}");
            sb.AppendLine($"修改时间：{info.LastWriteTime}");

            // 检查文件权限（简化）
            var canRead = info.Exists;
            var canWrite = (info.Attributes & FileAttributes.ReadOnly) == 0;
            sb.AppendLine($"可读：{canRead}");
            sb.AppendLine($"可写：{canWrite}");
        }
        else if (Directory.Exists(filePath))
        {
            var info = new DirectoryInfo(filePath);
            sb.AppendLine($"目录：{info.FullName}");
            sb.AppendLine($"创建时间：{info.CreationTime}");

            var fileCount = Directory.GetFiles(filePath, "*.*", SearchOption.AllDirectories).Length;
            sb.AppendLine($"文件数量：{fileCount}");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> FullScanAsync(string directory, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return Fail($"目录不存在：{directory}");

        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        var sb = new StringBuilder();
        sb.AppendLine($"完整安全扫描：{directory}");
        sb.AppendLine($"扫描文件数：{files.Length}");
        sb.AppendLine();

        int totalVulnerabilities = 0;
        int totalSecrets = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(file).ToLower();
            if (!new[] { ".cs", ".js", ".ts", ".py", ".java", ".go", ".rb", ".php" }.Contains(ext))
                continue;

            try
            {
                var content = await File.ReadAllTextAsync(file, ct);

                // 检查漏洞
                foreach (var (pattern, description) in VulnerabilityPatterns)
                {
                    var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                    if (matches.Count > 0)
                        totalVulnerabilities++;
                }

                // 检查敏感信息
                foreach (var pattern in SecretPatterns)
                {
                    var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                    if (matches.Count > 0)
                        totalSecrets++;
                }
            }
            catch { }
        }

        sb.AppendLine($"漏洞发现：{totalVulnerabilities} 个文件");
        sb.AppendLine($"敏感信息：{totalSecrets} 个文件");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static string MaskSecret(string secret)
    {
        if (secret.Length <= 8) return "***";
        return secret[..4] + "***" + secret[^4..];
    }

    private static ToolResult Fail(string error) => new() { Name = "security_scan", Success = false, Error = error };
}

/// <summary>
/// 安全发现
/// </summary>
public class SecurityFinding
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Evidence { get; set; } = string.Empty;
}
