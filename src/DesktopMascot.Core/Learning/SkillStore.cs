using System.Text;
using System.Text.Json;

namespace DesktopMascot.Core.Learning;

/// <summary>
/// 技能存储 — 持久化自动生成的技能到文件
/// </summary>
public class SkillStore
{
    private readonly string _skillsDir;
    private readonly object _lock = new();
    private List<SkillDefinition>? _cache;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SkillStore(string? skillsDirectory = null)
    {
        _skillsDir = skillsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "skills");
        Directory.CreateDirectory(_skillsDir);
    }

    /// <summary>
    /// 保存技能定义
    /// </summary>
    public void Save(SkillDefinition skill)
    {
        lock (_lock)
        {
            var skills = LoadAll();
            var existingIndex = skills.FindIndex(s => s.Id == skill.Id);
            if (existingIndex >= 0)
                skills[existingIndex] = skill;
            else
                skills.Add(skill);

            Persist(skills);
            _cache = skills;
        }
    }

    /// <summary>
    /// 批量保存
    /// </summary>
    public void SaveAll(List<SkillDefinition> skills)
    {
        lock (_lock)
        {
            Persist(skills);
            _cache = skills;
        }
    }

    /// <summary>
    /// 获取所有技能
    /// </summary>
    public List<SkillDefinition> GetAll()
    {
        lock (_lock)
        {
            _cache ??= LoadAll();
            return new List<SkillDefinition>(_cache);
        }
    }

    /// <summary>
    /// 按 ID 获取
    /// </summary>
    public SkillDefinition? GetById(string id)
    {
        lock (_lock)
        {
            var skills = GetAll();
            return skills.FirstOrDefault(s => s.Id == id);
        }
    }

    /// <summary>
    /// 按类型获取
    /// </summary>
    public List<SkillDefinition> GetByType(string taskType)
    {
        lock (_lock)
        {
            return GetAll().Where(s =>
                s.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    /// <summary>
    /// 删除技能
    /// </summary>
    public bool Delete(string id)
    {
        lock (_lock)
        {
            var skills = GetAll();
            var removed = skills.RemoveAll(s => s.Id == id);
            if (removed > 0)
            {
                Persist(skills);
                _cache = skills;
            }
            return removed > 0;
        }
    }

    /// <summary>
    /// 从任务历史自动生成技能并保存
    /// </summary>
    public int GenerateAndSaveFromHistory(List<DesktopMascot.Core.Storage.TaskHistoryRecord> history)
    {
        var engine = new LearningEngine();
        var generated = engine.GenerateSkillsFromHistory(history);

        if (generated.Count == 0) return 0;

        var existing = GetAll();
        var existingTypes = existing.Select(s => s.TaskType).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newSkills = new List<SkillDefinition>();

        foreach (var gen in generated)
        {
            if (existingTypes.Contains(gen.TaskType)) continue;

            newSkills.Add(new SkillDefinition
            {
                Id = $"skill_{gen.TaskType.ToLower()}_{DateTime.UtcNow:yyyyMMdd}",
                Name = gen.Name,
                Description = gen.Description,
                TaskType = gen.TaskType,
                SuccessRate = gen.SuccessRate,
                AverageDurationSeconds = gen.AverageDurationSeconds,
                TaskCount = gen.TaskCount,
                Source = "auto_generated",
                CreatedAt = DateTime.UtcNow
            });
        }

        if (newSkills.Count > 0)
        {
            var all = GetAll();
            all.AddRange(newSkills);
            SaveAll(all);
        }

        return newSkills.Count;
    }

    /// <summary>
    /// 导出为 Markdown
    /// </summary>
    public string ExportToMarkdown()
    {
        var skills = GetAll();
        var sb = new StringBuilder();
        sb.AppendLine("# 已生成技能列表");
        sb.AppendLine();
        sb.AppendLine($"总计：{skills.Count} 个技能");
        sb.AppendLine();

        foreach (var skill in skills.OrderByDescending(s => s.CreatedAt))
        {
            sb.AppendLine($"## {skill.Name}");
            sb.AppendLine();
            sb.AppendLine($"- **类型**：{skill.TaskType}");
            sb.AppendLine($"- **成功率**：{skill.SuccessRate:P0}");
            sb.AppendLine($"- **平均耗时**：{skill.AverageDurationSeconds:F1}s");
            sb.AppendLine($"- **使用次数**：{skill.TaskCount}");
            sb.AppendLine($"- **来源**：{skill.Source}");
            sb.AppendLine($"- **创建时间**：{skill.CreatedAt:yyyy-MM-dd}");
            sb.AppendLine($"- **描述**：{skill.Description}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private List<SkillDefinition> LoadAll()
    {
        var filePath = Path.Combine(_skillsDir, "skills.json");
        if (!File.Exists(filePath)) return new();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<SkillDefinition>>(json, JsonOptions) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private void Persist(List<SkillDefinition> skills)
    {
        var filePath = Path.Combine(_skillsDir, "skills.json");
        var json = JsonSerializer.Serialize(skills, JsonOptions);
        File.WriteAllText(filePath, json);
    }
}

/// <summary>
/// 技能定义 — 从任务历史自动生成的可复用技能
/// </summary>
public class SkillDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string TaskType { get; set; } = "";
    public float SuccessRate { get; set; }
    public double AverageDurationSeconds { get; set; }
    public int TaskCount { get; set; }
    public string Source { get; set; } = "manual";
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public int UseCount { get; set; }
}
