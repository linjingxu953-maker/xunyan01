using DesktopMascot.Core.Tools;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 任务模板生成器 — 预定义模板（周报/简历/PPT大纲/项目方案/日报/会议纪要）
/// </summary>
public class TaskTemplateTool : ITool
{
    public string Name => "task_template";
    public string Description => "任务模板：生成周报、日报、简历、PPT大纲、项目方案、会议纪要等结构化文档模板。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["generate", "list", "customize"], "description": "操作类型" },
            "template": { "type": "string", "enum": ["weekly_report", "daily_report", "resume", "ppt_outline", "project_plan", "meeting_minutes", "bug_report", "feature_spec", "one_pager", "retrospective"], "description": "模板类型" },
            "title": { "type": "string", "description": "文档标题" },
            "content": { "type": "string", "description": "自定义内容/补充信息" },
            "sections": { "type": "string", "description": "自定义章节（JSON数组）" },
            "output_path": { "type": "string", "description": "输出文件路径" }
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

            return action switch
            {
                "generate" => await GenerateTemplateAsync(root, ct),
                "list" => ListTemplates(),
                "customize" => await CustomizeTemplateAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"模板生成失败：{ex.Message}");
        }
    }

    private async Task<ToolResult> GenerateTemplateAsync(JsonElement root, CancellationToken ct)
    {
        var template = root.TryGetProperty("template", out var tEl) ? tEl.GetString() ?? "weekly_report" : "weekly_report";
        var title = root.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        var content = template switch
        {
            "weekly_report" => GenerateWeeklyReport(title),
            "daily_report" => GenerateDailyReport(title),
            "resume" => GenerateResume(title),
            "ppt_outline" => GeneratePptOutline(title),
            "project_plan" => GenerateProjectPlan(title),
            "meeting_minutes" => GenerateMeetingMinutes(title),
            "bug_report" => GenerateBugReport(title),
            "feature_spec" => GenerateFeatureSpec(title),
            "one_pager" => GenerateOnePager(title),
            "retrospective" => GenerateRetrospective(title),
            _ => GenerateWeeklyReport(title)
        };

        if (!string.IsNullOrEmpty(outputPath))
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(outputPath, content, ct);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"✅ 模板已生成：{GetTemplateName(template)}");
        if (!string.IsNullOrEmpty(outputPath))
            sb.AppendLine($"📁 已保存到：{outputPath}");
        sb.AppendLine();
        sb.AppendLine(content);

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private ToolResult ListTemplates()
    {
        var templates = new Dictionary<string, string>
        {
            ["weekly_report"] = "周报 — 本周工作汇总、下周计划",
            ["daily_report"] = "日报 — 今日工作、进度、问题",
            ["resume"] = "简历 — 个人信息、教育、经历、技能",
            ["ppt_outline"] = "PPT大纲 — 演讲结构、每页内容",
            ["project_plan"] = "项目方案 — 目标、范围、排期、资源",
            ["meeting_minutes"] = "会议纪要 — 参会人、议题、决议、待办",
            ["bug_report"] = "Bug报告 — 复现步骤、期望/实际、环境",
            ["feature_spec"] = "功能规格 — 需求、设计、验收标准",
            ["one_pager"] = "一页纸方案 — 核心价值、方案、收益",
            ["retrospective"] = "复盘总结 — 做得好/待改进/行动项"
        };

        var sb = new StringBuilder();
        sb.AppendLine("可用模板（共 10 种）：");
        sb.AppendLine();
        foreach (var (key, desc) in templates)
            sb.AppendLine($"  {key} — {desc}");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> CustomizeTemplateAsync(JsonElement root, CancellationToken ct)
    {
        var template = root.TryGetProperty("template", out var tEl) ? tEl.GetString() ?? "weekly_report" : "weekly_report";
        var title = root.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? "" : "";
        var content = root.TryGetProperty("content", out var cEl) ? cEl.GetString() ?? "" : "";
        var sectionsJson = root.TryGetProperty("sections", out var sEl) ? sEl.GetString() : null;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        List<string>? customSections = null;
        if (!string.IsNullOrEmpty(sectionsJson))
        {
            try { customSections = JsonSerializer.Deserialize<List<string>>(sectionsJson); }
            catch { }
        }

        // 生成模板并注入自定义内容
        var baseContent = template switch
        {
            "weekly_report" => GenerateWeeklyReport(title),
            "daily_report" => GenerateDailyReport(title),
            "resume" => GenerateResume(title),
            "ppt_outline" => GeneratePptOutline(title),
            "project_plan" => GenerateProjectPlan(title),
            "meeting_minutes" => GenerateMeetingMinutes(title),
            "bug_report" => GenerateBugReport(title),
            "feature_spec" => GenerateFeatureSpec(title),
            "one_pager" => GenerateOnePager(title),
            "retrospective" => GenerateRetrospective(title),
            _ => GenerateWeeklyReport(title)
        };

        // 如果有自定义章节，追加到末尾
        var sb = new StringBuilder();
        sb.AppendLine(baseContent);

        if (!string.IsNullOrEmpty(content))
        {
            sb.AppendLine();
            sb.AppendLine("## 补充内容");
            sb.AppendLine();
            sb.AppendLine(content);
        }

        if (customSections != null)
        {
            sb.AppendLine();
            foreach (var section in customSections)
            {
                sb.AppendLine($"## {section}");
                sb.AppendLine();
                sb.AppendLine("（请填写）");
                sb.AppendLine();
            }
        }

        if (!string.IsNullOrEmpty(outputPath))
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(outputPath, sb.ToString(), ct);
        }

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #region 模板生成

    private static string GenerateWeeklyReport(string title)
    {
        title = string.IsNullOrEmpty(title) ? $"周报 {DateTime.Now:yyyy-MM-dd}" : title;
        return $@"# {title}

> 日期：{DateTime.Now:yyyy-MM-dd}（第 {GetWeekNumber()} 周）

---

## 一、本周工作

| 序号 | 工作内容 | 完成状态 | 备注 |
|------|---------|---------|------|
| 1 | | | |
| 2 | | | |
| 3 | | | |

## 二、工作成果

- 

## 三、遇到的问题

| 问题 | 解决方案 | 状态 |
|------|---------|------|
| | | |

## 四、下周计划

| 序号 | 计划内容 | 优先级 | 预计时间 |
|------|---------|--------|---------|
| 1 | | 高 | |
| 2 | | 中 | |
| 3 | | 低 | |

## 五、需要协助

- 
";
    }

    private static string GenerateDailyReport(string title)
    {
        title = string.IsNullOrEmpty(title) ? $"日报 {DateTime.Now:MM-dd}" : title;
        return $@"# {title}

> {DateTime.Now:yyyy-MM-dd dddd}

---

## 今日工作

| 任务 | 进度 | 耗时 | 备注 |
|------|------|------|------|
| | | | |
| | | | |

## 今日成果

- 

## 遇到的问题

- 

## 明日计划

- 
";
    }

    private static string GenerateResume(string title)
    {
        title = string.IsNullOrEmpty(title) ? "个人简历" : title;
        return $@"# {title}

---

## 个人信息

- **姓名**：
- **电话**：
- **邮箱**：
- **GitHub**：

## 求职意向

- **目标岗位**：
- **期望城市**：
- **到岗时间**：

## 教育背景

| 时间 | 学校 | 专业 | 学历 |
|------|------|------|------|
| | | | |

## 工作经历

### 公司名 — 职位（起止时间）

- 负责：
- 成果：
- 技术栈：

## 项目经历

### 项目名称

- **简介**：
- **职责**：
- **成果**：
- **技术栈**：

## 专业技能

- 
- 
- 

## 自我评价

- 
";
    }

    private static string GeneratePptOutline(string title)
    {
        title = string.IsNullOrEmpty(title) ? "PPT 大纲" : title;
        return $@"# {title}

---

## 第 1 页：封面

- 标题：{title}
- 副标题：
- 演讲者：
- 日期：

## 第 2 页：目录

1. 背景介绍
2. 核心内容
3. 方案/案例
4. 总结与下一步

## 第 3 页：背景介绍

- 问题/痛点
- 现状分析
- 目标

## 第 4-6 页：核心内容

### 要点 1
- 

### 要点 2
- 

### 要点 3
- 

## 第 7 页：方案/案例

- 方案描述
- 预期效果
- 资源需求

## 第 8 页：总结

- 核心观点回顾
- 下一步计划
- Q&A

---
*建议每页控制在 5-7 行以内*
";
    }

    private static string GenerateProjectPlan(string title)
    {
        title = string.IsNullOrEmpty(title) ? "项目方案" : title;
        return $@"# {title}

---

## 1. 项目背景

### 1.1 问题描述


### 1.2 目标


### 1.3 范围

- 
- 
- 

## 2. 技术方案

### 2.1 整体架构


### 2.2 核心模块


### 2.3 技术选型

| 模块 | 技术 | 理由 |
|------|------|------|
| | | |

## 3. 项目排期

| 阶段 | 内容 | 时间 | 产出 |
|------|------|------|------|
| P1 | | | |
| P2 | | | |
| P3 | | | |

## 4. 资源需求

| 资源 | 数量 | 说明 |
|------|------|------|
| 人力 | | |
| 环境 | | |

## 5. 风险评估

| 风险 | 影响 | 应对 |
|------|------|------|
| | | |

## 6. 验收标准

- [ ] 
- [ ] 
- [ ] 
";
    }

    private static string GenerateMeetingMinutes(string title)
    {
        title = string.IsNullOrEmpty(title) ? $"会议纪要 {DateTime.Now:MM-dd}" : title;
        return $@"# {title}

> {DateTime.Now:yyyy-MM-dd HH:mm}

---

## 参会人员

- 
- 

## 会议议题

| 序号 | 议题 | 发言人 | 结论 |
|------|------|--------|------|
| 1 | | | |
| 2 | | | |

## 决议事项

| 序号 | 决议 | 负责人 | 截止时间 |
|------|------|--------|---------|
| 1 | | | |
| 2 | | | |

## 待办事项

- [ ] 

## 下次会议

- 时间：
- 议题：
";
    }

    private static string GenerateBugReport(string title)
    {
        title = string.IsNullOrEmpty(title) ? "Bug 报告" : title;
        return $@"# {title}

---

## 基本信息

- **提交人**：
- **日期**：{DateTime.Now:yyyy-MM-dd}
- **严重程度**：🔴 高 / 🟡 中 / 🟢 低
- **状态**：待修复

## 问题描述

简要描述 bug 表现：

## 复现步骤

1. 
2. 
3. 

## 期望行为


## 实际行为


## 环境信息

- **操作系统**：
- **浏览器/版本**：
- **应用版本**：

## 截图/录屏

（可选）

## 附加信息

（可选）
";
    }

    private static string GenerateFeatureSpec(string title)
    {
        title = string.IsNullOrEmpty(title) ? "功能规格" : title;
        return $@"# {title} — 功能规格

---

## 1. 需求概述

### 1.1 背景


### 1.2 目标用户


### 1.3 核心价值


## 2. 功能设计

### 2.1 用户故事

- 作为 [角色]，我想要 [功能]，以便 [价值]

### 2.2 功能列表

| 功能 | 优先级 | 说明 |
|------|--------|------|
| | P0 | |
| | P1 | |

### 2.3 交互流程

```
步骤1 → 步骤2 → 步骤3
```

## 3. 数据设计

### 3.1 数据模型


### 3.2 接口设计

| 接口 | 方法 | 参数 | 返回 |
|------|------|------|------|
| | | | |

## 4. 验收标准

- [ ] 
- [ ] 
- [ ] 

## 5. 排期

| 阶段 | 时间 | 产出 |
|------|------|------|
| 设计 | | |
| 开发 | | |
| 测试 | | |
";
    }

    private static string GenerateOnePager(string title)
    {
        title = string.IsNullOrEmpty(title) ? "一页纸方案" : title;
        return $@"# {title}

---

## 🎯 核心价值

**一句话描述**：

## 💡 问题与机会

- 
- 

## 🛠 解决方案

- 
- 

## 📊 预期收益

| 指标 | 当前 | 目标 |
|------|------|------|
| | | |

## 📅 里程碑

| 阶段 | 时间 | 产出 |
|------|------|------|
| P1 | | |
| P2 | | |

## ⚠️ 风险与依赖

- 

## 🤝 需要的资源

- 
";
    }

    private static string GenerateRetrospective(string title)
    {
        title = string.IsNullOrEmpty(title) ? $"复盘 {DateTime.Now:yyyy-MM-dd}" : title;
        return $@"# {title}

---

## 做得好的 ✅

| 事项 | 原因 | 可复用 |
|------|------|--------|
| | | |
| | | |

## 待改进的 ⚠️

| 事项 | 原因 | 改进方案 |
|------|------|---------|
| | | |
| | | |

## 行动项 📋

| 行动 | 负责人 | 截止日期 |
|------|--------|---------|
| | | |
| | | |

## 量化数据

- **任务完成率**：
- **平均耗时**：
- **Bug 率**：

## 个人感悟

- 
";
    }

    #endregion

    #region Helpers

    private static string GetTemplateName(string template) => template switch
    {
        "weekly_report" => "周报",
        "daily_report" => "日报",
        "resume" => "简历",
        "ppt_outline" => "PPT大纲",
        "project_plan" => "项目方案",
        "meeting_minutes" => "会议纪要",
        "bug_report" => "Bug报告",
        "feature_spec" => "功能规格",
        "one_pager" => "一页纸方案",
        "retrospective" => "复盘总结",
        _ => "模板"
    };

    private static int GetWeekNumber()
    {
        var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
        return cal.GetWeekOfYear(DateTime.Now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
    }

    private static ToolResult Fail(string error) => new() { Name = "task_template", Success = false, Error = error };

    #endregion
}
