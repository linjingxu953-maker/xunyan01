using DesktopMascot.Core.Tools;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 安全扫描工具增强版 — 多维度漏洞检测 + 敏感信息 + 依赖安全 + 配置审计
/// </summary>
public class SecurityScanTool : ITool
{
    public string Name => "security_scan";
    public string Description => "安全扫描：漏洞检测、敏感信息泄露、依赖安全、配置审计、权限检查。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["vulnerabilities", "secrets", "permissions", "full", "dependencies", "config_audit"], "description": "扫描类型" },
            "file_path": { "type": "string", "description": "文件路径" },
            "directory": { "type": "string", "description": "目录路径" },
            "severity": { "type": "string", "enum": ["low", "medium", "high", "critical", "all"], "description": "最低严重级别过滤" },
            "extensions": { "type": "string", "description": "要扫描的文件扩展名，逗号分隔（默认 .cs,.js,.ts,.py,.java,.go,.rb,.php,.json,.xml,.yaml,.yml,.config）" }
        },
        "required": ["action"]
    }
    """;

    #region 漏洞模式库

    private static readonly (string Pattern, string Description, string Severity, string Category)[] VulnerabilityPatterns = new[]
    {
        // 代码注入
        (@"eval\s*\(", "eval 代码注入", "high", "injection"),
        (@"exec\s*\(", "exec 命令注入", "high", "injection"),
        (@"system\s*\(", "system 命令执行", "high", "injection"),
        (@"subprocess\.(?:call|Popen|run)\s*\(", "子进程命令执行", "high", "injection"),
        (@"Runtime\.exec\s*\(", "Java Runtime 命令执行", "high", "injection"),
        (@"os\.system\s*\(", "Python os.system 命令注入", "high", "injection"),
        (@"os\.popen\s*\(", "Python os.popen 命令注入", "high", "injection"),

        // XSS
        (@"innerHTML\s*=", "innerHTML XSS 风险", "high", "xss"),
        (@"outerHTML\s*=", "outerHTML XSS 风险", "high", "xss"),
        (@"document\.write\s*\(", "document.write XSS 风险", "high", "xss"),
        (@"\$\([^)]*\)\.html\s*\(", "jQuery html() XSS 风险", "medium", "xss"),
        (@"dangerouslySetInnerHTML", "React dangerouslySetInnerHTML", "high", "xss"),
        (@"v-html\s*=", "Vue v-html XSS 风险", "high", "xss"),
        (@"\[\(innerHTML\)\]", "Angular innerHTML 绑定", "high", "xss"),

        // SQL 注入
        (@"SELECT\s+.*FROM\s+.*WHERE\s+.*\+", "SQL 字符串拼接", "critical", "sql_injection"),
        (@"SELECT\s+.*FROM\s+.*WHERE\s+.*\$", "SQL 模板字符串注入", "critical", "sql_injection"),
        (@"\.Execute\s*\(", "直接 SQL 执行", "medium", "sql_injection"),
        (@"ExecuteNonQuery\s*\(", "SQL 非查询执行", "medium", "sql_injection"),
        (@"query\s*\(\s*[`""]", "SQL 字面量查询", "medium", "sql_injection"),
        (@"DbContext.*Raw\s*\(", "EF Core Raw SQL", "medium", "sql_injection"),

        // 路径遍历
        (@"\.\./\.\./", "路径遍历尝试", "high", "path_traversal"),
        (@"Path\.Combine\s*\(.*\.\.", "路径组合含上级目录", "medium", "path_traversal"),
        (@"ReadAllText\s*\(.*\+", "文件读取路径拼接", "medium", "path_traversal"),
        (@"File\.Read\s*\(.*\+", "文件读取路径拼接", "medium", "path_traversal"),

        // 反序列化
        (@"BinaryFormatter\.Deserialize", "BinaryFormatter 不安全反序列化", "critical", "deserialization"),
        (@"JsonConvert\.DeserializeObject\s*\(", "JSON 反序列化（检查类型约束）", "low", "deserialization"),
        (@"XmlSerializer\.Deserialize\s*\(", "XML 反序列化", "low", "deserialization"),
        (@"JavaScriptSerializer\.Deserialize", "JavaScriptSerializer 反序列化", "high", "deserialization"),
        (@"pickle\.loads?\s*\(", "Python pickle 反序列化", "critical", "deserialization"),
        (@"yaml\.load\s*\([^)]*Loader\s*=\s*yaml\.FullLoader", "YAML FullLoader 加载", "high", "deserialization"),

        // SSRF
        (@"WebClient\.DownloadString\s*\(", "WebClient 下载（检查 URL 来源）", "medium", "ssrf"),
        (@"HttpClient\.GetAsync\s*\(", "HTTP 请求（检查 URL 来源）", "low", "ssrf"),
        (@"requests\.get\s*\(", "Python requests GET（检查 URL）", "low", "ssrf"),
        (@"fetch\s*\([^)]*http", "Fetch HTTP 请求", "low", "ssrf"),

        // 加密弱点
        (@"MD5\.Create\s*\(", "MD5 弱哈希", "medium", "crypto_weak"),
        (@"SHA1\.Create\s*\(", "SHA1 弱哈希", "medium", "crypto_weak"),
        (@"DES\.Create\s*\(", "DES 弱加密", "high", "crypto_weak"),
        (@"RC2\.Create\s*\(", "RC2 弱加密", "high", "crypto_weak"),
        (@"RandomNumberGenerator\.Create\s*\(\)", "非推荐随机数生成方式", "low", "crypto_weak"),
        (@"new\s+Random\s*\(", "不安全随机数", "medium", "crypto_weak"),

        // 硬编码凭证
        (@"ConnectString\s*=.*Password", "数据库连接字符串含密码", "high", "hardcoded_creds"),
        (@"connectionString.*password", "连接字符串密码", "high", "hardcoded_creds"),

        // 权限/授权
        (@"AllowAnonymous", "匿名访问标记", "medium", "auth"),
        (@"\[Authorize\].*\[AllowAnonymous\]", "授权后匿名覆盖", "high", "auth"),
        (@"CORS.*AllowAnyOrigin", "CORS 允许任意来源", "high", "auth"),
        (@"CORS.*\*", "CORS 通配符", "high", "auth"),

        // 调试信息泄露
        (@"stackTrace\s*=|StackTrace", "堆栈跟踪暴露", "low", "info_leak"),
        (@"Exception\.Message\s*\+|\.ToString\(\)\s*\+.*log", "异常信息泄露", "medium", "info_leak"),
        (@"Debug\.Write|Console\.Write.*password|Console\.Write.*secret", "调试输出敏感信息", "high", "info_leak"),

        // 输入验证
        (@"RegularExpression\s*=.*\^.*\$.*IgnoreCase", "宽松正则验证", "low", "validation"),
        (@"ModelState\.IsValid.*return", "模型验证绕过风险", "medium", "validation"),

        // 文件操作
        (@"File\.Delete\s*\(", "文件删除操作", "low", "file_ops"),
        (@"Directory\.Delete\s*\(", "目录删除操作", "medium", "file_ops"),
        (@"File\.WriteAllText\s*\(.*\+", "文件写入路径拼接", "medium", "file_ops"),

        // 并发安全
        (@"static\s+.*Dictionary|static\s+.*List", "静态集合并发风险", "medium", "concurrency"),
        (@"Thread\.Sleep\s*\(", "Thread.Sleep 阻塞", "low", "concurrency"),
    };

    #endregion

    #region 敏感信息模式库

    private static readonly (string Pattern, string Description, string Category)[] SecretPatterns = new[]
    {
        // API Keys
        (@"api[_-]?key\s*[=:]\s*['""][^'""]{8,}['""]", "API Key", "api_key"),
        (@"sk-[0-9a-zA-Z]{32,}", "OpenAI API Key", "api_key"),
        (@"sk-proj-[0-9a-zA-Z]{32,}", "OpenAI Project Key", "api_key"),
        (@"xai-[0-9a-zA-Z]{32,}", "xAI API Key", "api_key"),
        (@"AKIA[0-9A-Z]{16}", "AWS Access Key", "cloud_creds"),
        (@"ghp_[0-9a-zA-Z]{36}", "GitHub Personal Access Token", "token"),
        (@"gho_[0-9a-zA-Z]{36}", "GitHub OAuth Token", "token"),
        (@"glpat-[0-9a-zA-Z\-]{20,}", "GitLab PAT", "token"),
        (@"xox[bpsa]-[0-9a-zA-Z\-]+", "Slack Token", "token"),
        (@"AIza[0-9A-Za-z\-_]{35}", "Google API Key", "api_key"),

        // 密码
        (@"password\s*[=:]\s*['""][^'""]{4,}['""]", "密码", "password"),
        (@"passwd\s*[=:]\s*['""][^'""]{4,}['""]", "密码（passwd）", "password"),
        (@"pwd\s*[=:]\s*['""][^'""]{4,}['""]", "密码（pwd）", "password"),
        (@"secret\s*[=:]\s*['""][^'""]{8,}['""]", "密钥", "secret"),
        (@"token\s*[=:]\s*['""][^'""]{8,}['""]", "Token", "token"),
        (@"access_token\s*[=:]\s*['""][^'""]{8,}['""]", "Access Token", "token"),
        (@"refresh_token\s*[=:]\s*['""][^'""]{8,}['""]", "Refresh Token", "token"),

        // 数据库连接
        (@"Server=[^;]+;.*Password=[^;]+", "数据库连接字符串", "connection_string"),
        (@"mongodb(\+srv)?://[^/\s]+:[^@]+@", "MongoDB 连接字符串", "connection_string"),
        (@"postgres(ql)?://[^/\s]+:[^@]+@", "PostgreSQL 连接字符串", "connection_string"),
        (@"mysql://[^/\s]+:[^@]+@", "MySQL 连接字符串", "connection_string"),
        (@"redis://[^/\s]+:[^@]+@", "Redis 连接字符串", "connection_string"),

        // 私钥
        (@"-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----", "私钥文件", "private_key"),
        (@"-----BEGIN\s+ENCRYPTED\s+PRIVATE\s+KEY-----", "加密私钥", "private_key"),

        // JWT
        (@"eyJ[A-Za-z0-9\-_]+\.eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+", "JWT Token", "token"),

        // 云服务
        (@"AccountKey=[A-Za-z0-9+/=]{44}", "Azure Storage Account Key", "cloud_creds"),
        (@"(?i)client_secret\s*[=:]\s*['""][^'""]{8,}['""]", "OAuth Client Secret", "oauth"),
    };

    #endregion

    #region 配置安全模式

    private static readonly (string Pattern, string Description, string Severity)[] ConfigPatterns = new[]
    {
        (@"Debug\s*=\s*true", "调试模式开启", "medium"),
        (@"DebugMode\s*=\s*true", "调试模式开启", "medium"),
        (@"Development.*Environment", "开发环境配置", "low"),
        (@"<compilation\s+debug\s*=\s*""true""", "编译调试模式", "medium"),
        (@"trace.*true|Trace.*true", "跟踪日志开启", "low"),
        (@"logging.*debug|LogLevel.*Debug", "Debug 级别日志", "low"),
        (@"(?i)ssl\s*=\s*false|UseSsl\s*=\s*false|SslMode\s*=\s*None", "SSL 禁用", "high"),
        (@"(?i)verify\s*=\s*false|ValidateCertificate\s*=\s*false|ServerCertificateValidationCallback.*return\s+true", "证书验证禁用", "critical"),
    };

    #endregion

    #region 依赖安全模式

    private static readonly (string Pattern, string Description)[] DependencyPatterns = new[]
    {
        // 已知不安全的包
        (@"Newtonsoft\.Json.*1[0-2]\.", "Newtonsoft.Json 旧版本（<13 有 CVE）"),
        (@"System\.Net\.Http\.WinHttpHandler.*5\.0\.", "WinHttpHandler 旧版本"),
        (@"System\.Text\.Encodings\.Web.*[0-6]\.", "Text.Encodings.Web 旧版本"),
    };

    private static readonly string[] DependencyFileNames = new[]
    {
        "package.json", "package-lock.json", "yarn.lock", "pnpm-lock.yaml",
        "requirements.txt", "Pipfile", "Pipfile.lock", "poetry.lock",
        "go.sum", "Cargo.lock", "Gemfile.lock",
        "*.csproj", "packages.config", "Directory.Packages.props"
    };

    private static readonly string[] DangerousNpmPackages = new[]
    {
        "event-stream", "flatmap-stream", "mailparser", "nodemailer",
        "ua-parser-js", "coa", "rc", "colors", "faker",
    };

    #endregion

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";
            var filePath = root.TryGetProperty("file_path", out var fEl) ? fEl.GetString() ?? "" : "";
            var directory = root.TryGetProperty("directory", out var dEl) ? dEl.GetString() ?? "" : "";
            var severityFilter = root.TryGetProperty("severity", out var sEl) ? sEl.GetString() ?? "all" : "all";
            var extensions = root.TryGetProperty("extensions", out var eEl) ? eEl.GetString() : null;

            var extList = ParseExtensions(extensions);

            return action switch
            {
                "vulnerabilities" => await ScanVulnerabilitiesAsync(filePath, severityFilter, ct),
                "secrets" => await ScanSecretsAsync(filePath, ct),
                "permissions" => await ScanPermissionsAsync(filePath, ct),
                "full" => await FullScanAsync(directory, severityFilter, extList, ct),
                "dependencies" => await ScanDependenciesAsync(directory, ct),
                "config_audit" => await ConfigAuditAsync(directory, ct),
                _ => Fail($"不支持的扫描类型：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"安全扫描失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> ScanVulnerabilitiesAsync(string filePath, string severityFilter, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var findings = FilterBySeverity(
            ScanContentForVulnerabilities(content, filePath),
            severityFilter);

        return FormatScanResult($"漏洞扫描：{filePath}", findings);
    }

    private async Task<ToolResult> ScanSecretsAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath)) return Fail($"文件不存在：{filePath}");

        var content = await File.ReadAllTextAsync(filePath, ct);
        var findings = ScanContentForSecrets(content, filePath);

        return FormatScanResult($"敏感信息扫描：{filePath}", findings);
    }

    private async Task<ToolResult> ScanPermissionsAsync(string filePath, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"权限检查：{filePath}");

        if (File.Exists(filePath))
        {
            var info = new FileInfo(filePath);
            sb.AppendLine($"  文件：{info.FullName}");
            sb.AppendLine($"  大小：{info.Length:N0} 字节");
            sb.AppendLine($"  创建：{info.CreationTime:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"  修改：{info.LastWriteTime:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"  只读：{info.IsReadOnly}");
            sb.AppendLine($"  隐藏：{(info.Attributes & FileAttributes.Hidden) != 0}");

            // 检查扩展名风险
            var ext = info.Extension.ToLower();
            if (new[] { ".exe", ".dll", ".bat", ".cmd", ".ps1", ".sh" }.Contains(ext))
                sb.AppendLine("  ⚠ 可执行文件，检查来源");

            if (new[] { ".key", ".pem", ".pfx", ".p12", ".jks" }.Contains(ext))
                sb.AppendLine("  ⚠ 私钥/证书文件，确保不被提交");
        }
        else if (Directory.Exists(filePath))
        {
            var info = new DirectoryInfo(filePath);
            sb.AppendLine($"  目录：{info.FullName}");
            sb.AppendLine($"  创建：{info.CreationTime:yyyy-MM-dd HH:mm}");

            var files = Directory.GetFiles(filePath, "*.*", SearchOption.AllDirectories);
            var dirs = Directory.GetDirectories(filePath, "*.*", SearchOption.AllDirectories);
            sb.AppendLine($"  文件：{files.Length}");
            sb.AppendLine($"  子目录：{dirs.Length}");

            // 检查敏感文件
            var sensitiveFiles = files.Where(f =>
            {
                var name = Path.GetFileName(f).ToLower();
                return name is ".env" or "appsettings.json" or "web.config" or "app.config"
                    or ".gitignore" or "docker-compose.yml" or ".dockerignore";
            }).ToList();

            if (sensitiveFiles.Count > 0)
            {
                sb.AppendLine("  ⚠ 敏感配置文件：");
                foreach (var sf in sensitiveFiles)
                    sb.AppendLine($"    - {Path.GetRelativePath(filePath, sf)}");
            }
        }
        else
        {
            sb.AppendLine("  路径不存在");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> FullScanAsync(string directory, string severityFilter, HashSet<string> extensions, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return Fail($"目录不存在：{directory}");

        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
            .ToArray();

        var allFindings = new List<SecurityFinding>();
        int filesScanned = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                allFindings.AddRange(ScanContentForVulnerabilities(content, file));
                allFindings.AddRange(ScanContentForSecrets(content, file));
                filesScanned++;
            }
            catch { }
        }

        var filtered = FilterBySeverity(allFindings, severityFilter);

        var sb = new StringBuilder();
        sb.AppendLine($"完整安全扫描：{directory}");
        sb.AppendLine($"扫描文件：{filesScanned} / {files.Length}");
        sb.AppendLine($"发现问题：{filtered.Count}");
        sb.AppendLine($"  严重：{filtered.Count(f => f.Severity == "critical")}");
        sb.AppendLine($"  高危：{filtered.Count(f => f.Severity == "high")}");
        sb.AppendLine($"  中危：{filtered.Count(f => f.Severity == "medium")}");
        sb.AppendLine($"  低危：{filtered.Count(f => f.Severity == "low")}");
        sb.AppendLine();

        // 按类别汇总
        var categories = filtered.GroupBy(f => f.Category).OrderByDescending(g => g.Count());
        foreach (var cat in categories)
        {
            sb.AppendLine($"  [{cat.Key}] {cat.Count()} 个");
            foreach (var f in cat.Take(3))
                sb.AppendLine($"    行 {f.Line}: {f.Description} ({f.File})");
            if (cat.Count() > 3)
                sb.AppendLine($"    ... 还有 {cat.Count() - 3} 个");
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ScanDependenciesAsync(string directory, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return Fail($"目录不存在：{directory}");

        var sb = new StringBuilder();
        sb.AppendLine("依赖安全检查");
        sb.AppendLine();

        // package.json 检查
        var packageJsonPath = Path.Combine(directory, "package.json");
        if (File.Exists(packageJsonPath))
        {
            sb.AppendLine("Node.js 依赖：");
            try
            {
                var json = await File.ReadAllTextAsync(packageJsonPath, ct);
                var pkgDoc = JsonDocument.Parse(json);

                if (pkgDoc.RootElement.TryGetProperty("dependencies", out var deps))
                {
                    foreach (var prop in deps.EnumerateObject())
                    {
                        if (DangerousNpmPackages.Contains(prop.Name))
                            sb.AppendLine($"  ⚠ 危险包：{prop.Name} ({prop.Value})");
                    }
                }
            }
            catch { }
        }

        // csproj 检查
        var csprojFiles = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories);
        foreach (var csproj in csprojFiles)
        {
            var content = await File.ReadAllTextAsync(csproj, ct);
            foreach (var (pattern, description) in DependencyPatterns)
            {
                if (Regex.IsMatch(content, pattern))
                    sb.AppendLine($"  ⚠ {Path.GetFileName(csproj)}: {description}");
            }
        }

        if (sb.Length <= "依赖安全检查\n\n".Length)
            sb.AppendLine("  未发现已知不安全依赖");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> ConfigAuditAsync(string directory, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return Fail($"目录不存在：{directory}");

        var findings = new List<SecurityFinding>();
        var configFiles = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var name = Path.GetFileName(f).ToLower();
                return name is "appsettings.json" or "appsettings.development.json"
                    or "web.config" or "app.config" or ".env" or ".env.local"
                    or "docker-compose.yml" or "docker-compose.yaml"
                    or "launchSettings.json" or "settings.gradle"
                    or "config.yaml" or "config.yml" or "config.json";
            })
            .ToArray();

        foreach (var file in configFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, ct);
                foreach (var (pattern, description, severity) in ConfigPatterns)
                {
                    var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                    foreach (Match m in matches)
                    {
                        var lineNum = content[..m.Index].Count(c => c == '\n') + 1;
                        findings.Add(new SecurityFinding
                        {
                            Type = "config",
                            Severity = severity,
                            Description = description,
                            File = file,
                            Line = lineNum,
                            Evidence = m.Value.Length > 60 ? m.Value[..60] + "..." : m.Value,
                            Category = "config_audit"
                        });
                    }
                }
            }
            catch { }
        }

        var sb = new StringBuilder();
        sb.AppendLine("配置安全审计");
        sb.AppendLine($"检查配置文件：{configFiles.Length}");
        sb.AppendLine($"发现问题：{findings.Count}");
        sb.AppendLine();

        foreach (var f in findings.OrderByDescending(f => SeverityOrder(f.Severity)))
        {
            sb.AppendLine($"  [{f.Severity}] {Path.GetFileName(f.File)}:{f.Line} — {f.Description}");
        }

        if (findings.Count == 0)
            sb.AppendLine("  未发现配置安全问题");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #region 扫描逻辑

    private static List<SecurityFinding> ScanContentForVulnerabilities(string content, string filePath)
    {
        var findings = new List<SecurityFinding>();

        foreach (var (pattern, description, severity, category) in VulnerabilityPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var lineNum = content[..m.Index].Count(c => c == '\n') + 1;
                findings.Add(new SecurityFinding
                {
                    Type = "vulnerability",
                    Severity = severity,
                    Description = description,
                    File = filePath,
                    Line = lineNum,
                    Evidence = m.Value.Length > 80 ? m.Value[..80] + "..." : m.Value,
                    Category = category
                });
            }
        }

        return findings;
    }

    private static List<SecurityFinding> ScanContentForSecrets(string content, string filePath)
    {
        var findings = new List<SecurityFinding>();

        foreach (var (pattern, description, category) in SecretPatterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                // 跳过测试文件中的硬编码值
                if (IsTestFile(filePath)) continue;

                var lineNum = content[..m.Index].Count(c => c == '\n') + 1;
                findings.Add(new SecurityFinding
                {
                    Type = "secret",
                    Severity = "high",
                    Description = description,
                    File = filePath,
                    Line = lineNum,
                    Evidence = MaskSecret(m.Value),
                    Category = category
                });
            }
        }

        return findings;
    }

    private static bool IsTestFile(string filePath)
    {
        var lower = filePath.ToLowerInvariant();
        return lower.Contains("\\test\\") || lower.Contains("\\tests\\")
            || lower.Contains("\\testing\\") || lower.Contains("\\spec\\")
            || lower.Contains("\\mock\\") || lower.Contains("\\fixture\\")
            || lower.Contains(".test.") || lower.Contains(".spec.")
            || lower.Contains("_test.") || lower.Contains("_spec.");
    }

    private static List<SecurityFinding> FilterBySeverity(List<SecurityFinding> findings, string severityFilter)
    {
        if (severityFilter == "all") return findings;

        var minSeverity = severityFilter.ToLower() switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" => 2,
            "low" => 3,
            _ => 4
        };

        return findings.Where(f => SeverityOrder(f.Severity) <= minSeverity).ToList();
    }

    private static int SeverityOrder(string severity) => severity.ToLower() switch
    {
        "critical" => 0,
        "high" => 1,
        "medium" => 2,
        "low" => 3,
        _ => 4
    };

    private static HashSet<string> ParseExtensions(string? extensions)
    {
        var defaultExts = new HashSet<string>
        {
            ".cs", ".js", ".ts", ".py", ".java", ".go", ".rb", ".php",
            ".json", ".xml", ".yaml", ".yml", ".config", ".cshtml", ".razor"
        };

        if (string.IsNullOrWhiteSpace(extensions))
            return defaultExts;

        var result = new HashSet<string>();
        foreach (var ext in extensions.Split(','))
        {
            var trimmed = ext.Trim();
            if (!trimmed.StartsWith('.'))
                trimmed = "." + trimmed;
            result.Add(trimmed.ToLower());
        }

        return result.Count > 0 ? result : defaultExts;
    }

    #endregion

    private static ToolResult FormatScanResult(string title, List<SecurityFinding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{title}");
        sb.AppendLine($"发现问题：{findings.Count}");
        sb.AppendLine($"发现 {findings.Count} 个潜在问题");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("  未发现问题");
        }
        else
        {
            foreach (var f in findings.OrderByDescending(f => SeverityOrder(f.Severity)))
            {
                sb.AppendLine($"  [{f.Severity}] 行 {f.Line}: {f.Description}");
                sb.AppendLine($"    代码：{f.Evidence}");
            }
        }

        return new ToolResult { Name = "security_scan", Success = true, Content = sb.ToString() };
    }

    private static string MaskSecret(string secret)
    {
        if (secret.Length <= 8) return "***";
        if (secret.Length <= 16) return secret[..2] + "***" + secret[^2..];
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
    public string Category { get; set; } = string.Empty;
}
