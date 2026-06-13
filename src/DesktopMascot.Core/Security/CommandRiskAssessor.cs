using System.Text.RegularExpressions;
using DesktopMascot.Core.Enums;

namespace DesktopMascot.Core.Security;

/// <summary>
/// 命令风险评估器
/// </summary>
public class CommandRiskAssessor
{
    /// <summary>
    /// 危险命令黑名单
    /// </summary>
    private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "del", "rmdir", "rd", "rm", "rmrf",
        "format", "fdisk", "diskpart",
        "shutdown", "restart", "logoff",
        "taskkill", "taskkill /f",
        "net user", "net group",
        "reg delete", "regdel",
        "icacls", "takeown",
        "cipher /w",
        "format c:", "format d:"
    };

    /// <summary>
    /// 高风险命令模式
    /// </summary>
    private static readonly List<(Regex Pattern, string Reason)> HighRiskPatterns = new()
    {
        (new Regex(@"rm\s+-rf\s+/", RegexOptions.IgnoreCase), "递归删除根目录"),
        (new Regex(@"del\s+/[sfq]\s+\\\*", RegexOptions.IgnoreCase), "强制删除系统文件"),
        (new Regex(@"Remove-Item\s+-Recurse\s+-Force\s+[A-Z]:\\", RegexOptions.IgnoreCase), "递归删除驱动器内容"),
        (new Regex(@">\s*/dev/sd[a-z]", RegexOptions.IgnoreCase), "直接写入磁盘"),
        (new Regex(@"wget.*\|\s*sh", RegexOptions.IgnoreCase), "下载并执行脚本"),
        (new Regex(@"curl.*\|\s*bash", RegexOptions.IgnoreCase), "下载并执行脚本"),
        (new Regex(@"eval\s*\(", RegexOptions.IgnoreCase), "动态执行代码"),
    };

    /// <summary>
    /// 中风险命令模式
    /// </summary>
    private static readonly List<(Regex Pattern, string Reason)> MediumRiskPatterns = new()
    {
        (new Regex(@"npm\s+install\s+-g", RegexOptions.IgnoreCase), "全局安装包"),
        (new Regex(@"pip\s+install\s+--user", RegexOptions.IgnoreCase), "用户级安装包"),
        (new Regex(@"git\s+push\s+--force", RegexOptions.IgnoreCase), "强制推送"),
        (new Regex(@"git\s+reset\s+--hard", RegexOptions.IgnoreCase), "硬重置"),
        (new Regex(@"docker\s+rm", RegexOptions.IgnoreCase), "删除容器"),
        (new Regex(@"kubectl\s+delete", RegexOptions.IgnoreCase), "删除 K8s 资源"),
    };

    /// <summary>
    /// 评估命令风险
    /// </summary>
    public CommandRiskAssessment Assess(string command)
    {
        var assessment = new CommandRiskAssessment
        {
            Command = command,
            RiskLevel = PermissionLevel.L0_Chat
        };

        // 检查黑名单
        var commandBase = command.Split(' ')[0].ToLower();
        if (BlockedCommands.Contains(commandBase) || 
            BlockedCommands.Any(b => command.ToLower().Contains(b)))
        {
            assessment.IsBlocked = true;
            assessment.BlockReason = "命令在黑名单中";
            assessment.RiskLevel = PermissionLevel.L6_Forbidden;
            return assessment;
        }

        // 检查高风险模式
        foreach (var (pattern, reason) in HighRiskPatterns)
        {
            if (pattern.IsMatch(command))
            {
                assessment.IsBlocked = true;
                assessment.BlockReason = reason;
                assessment.RiskLevel = PermissionLevel.L6_Forbidden;
                return assessment;
            }
        }

        // 检查中风险模式
        foreach (var (pattern, reason) in MediumRiskPatterns)
        {
            if (pattern.IsMatch(command))
            {
                assessment.Warnings.Add(reason);
                assessment.RiskLevel = PermissionLevel.L5_CommandExec;
            }
        }

        // 基本命令执行总是需要确认
        if (assessment.RiskLevel < PermissionLevel.L5_CommandExec)
        {
            assessment.RiskLevel = PermissionLevel.L5_CommandExec;
        }

        return assessment;
    }

    /// <summary>
    /// 评估文件写入风险
    /// </summary>
    public PermissionLevel AssessFileWrite(string filePath)
    {
        // 系统目录高风险
        var systemPaths = new[]
        {
            @"C:\Windows",
            @"C:\Program Files",
            @"C:\Program Files (x86)",
            @"C:\System",
            @"C:\Boot"
        };

        foreach (var sysPath in systemPaths)
        {
            if (filePath.StartsWith(sysPath, StringComparison.OrdinalIgnoreCase))
            {
                return PermissionLevel.L6_Forbidden;
            }
        }

        // 其他路径需要写入确认
        return PermissionLevel.L4_FileWrite;
    }

    /// <summary>
    /// 评估文件读取风险
    /// </summary>
    public PermissionLevel AssessFileRead(string filePath)
    {
        // 敏感文件
        var sensitivePatterns = new[]
        {
            @"\.env",
            @"password",
            @"secret",
            @"credential",
            @"private.*key",
            @"\.pem$",
            @"\.key$"
        };

        foreach (var pattern in sensitivePatterns)
        {
            if (Regex.IsMatch(filePath, pattern, RegexOptions.IgnoreCase))
            {
                return PermissionLevel.L4_FileWrite; // 敏感文件读取需要确认
            }
        }

        return PermissionLevel.L3_FileRead;
    }
}
