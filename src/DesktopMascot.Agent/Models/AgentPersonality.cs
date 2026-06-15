namespace DesktopMascot.Agent.Models;

/// <summary>
/// Agent 语气配置
/// </summary>
public class AgentPersonality
{
    /// <summary>角色名称</summary>
    public string Name { get; set; } = "小桌灵";

    /// <summary>角色描述</summary>
    public string Description { get; set; } = "桌面AI工作助手";

    /// <summary>语气风格</summary>
    public ToneStyle Tone { get; set; } = ToneStyle.Friendly;

    /// <summary>语言风格</summary>
    public LanguageStyle Language { get; set; } = LanguageStyle.Standard;

    /// <summary>性格特征列表</summary>
    public List<string> Traits { get; set; } = new() { "沉稳", "可靠", "专业" };

    /// <summary>口头禅</summary>
    public string Catchphrase { get; set; } = "我在桌面待命，随时可以接任务。";

    /// <summary>回复长度偏好</summary>
    public ResponseLength LengthPreference { get; set; } = ResponseLength.Balanced;

    /// <summary>是否使用 emoji</summary>
    public bool UseEmoji { get; set; } = false;

    /// <summary>自定义系统提示词后缀</summary>
    public string? CustomSystemPromptSuffix { get; set; }

    /// <summary>
    /// 生成系统提示词
    /// </summary>
    public string BuildSystemPrompt(string toolNames, bool hasContext = false)
    {
        var tonePrompt = GetTonePrompt();
        var languagePrompt = GetLanguagePrompt();
        var lengthPrompt = GetLengthPrompt();

        var prompt = $@"你是 {Name}，{Description}。

## 语气和风格
{tonePrompt}

## 语言风格
{languagePrompt}

## 回复长度
{lengthPrompt}

## 可用工具
{toolNames}

## 工作原则
1. 准确理解用户意图
2. 高效完成任务
3. 主动提供有价值的信息
4. 遇到不确定的情况如实说明
5. 安全第一，敏感操作需要确认";

        if (hasContext)
        {
            prompt += "\n\n## 上下文感知\n你能够看到用户的屏幕内容，基于当前上下文提供更精准的帮助。";
        }

        if (!string.IsNullOrEmpty(CustomSystemPromptSuffix))
        {
            prompt += $"\n\n{CustomSystemPromptSuffix}";
        }

        return prompt;
    }

    private string GetTonePrompt()
    {
        return Tone switch
        {
            ToneStyle.Friendly => "你友善、耐心、乐于助人。使用温和的语气，像朋友一样交流。",
            ToneStyle.Professional => "你专业、严谨、高效。使用正式的商务语气，直接切入主题。",
            ToneStyle.Casual => "你轻松、随和、幽默。使用口语化的表达，可以适当开玩笑。",
            ToneStyle.Cute => "你可爱、活泼、元气满满。使用可爱的语气词，表达热情。",
            ToneStyle.Calm => "你沉稳、冷静、理性。使用平稳的语气，逻辑清晰地分析问题。",
            ToneStyle.Sarcastic => "你机智、犀利、带点讽刺。用幽默的方式指出问题，但不伤人。",
            _ => "你友善、专业、乐于助人。"
        };
    }

    private string GetLanguagePrompt()
    {
        return Language switch
        {
            LanguageStyle.Standard => "使用标准的中文表达，语法规范，用词准确。",
            LanguageStyle.Concise => "使用简洁的表达，尽量用最少的字说清楚。避免冗余。",
            LanguageStyle.Detailed => "使用详细的表达，提供充分的解释和背景信息。",
            LanguageStyle.Technical => "使用技术术语，表达精确，适合开发者交流。",
            LanguageStyle.Colloquial => "使用口语化的表达，像日常聊天一样自然。",
            _ => "使用标准的中文表达。"
        };
    }

    private string GetLengthPrompt()
    {
        return LengthPreference switch
        {
            ResponseLength.Short => "回复尽量简短，控制在 1-2 句话内。",
            ResponseLength.Balanced => "回复长度适中，根据问题复杂度调整。",
            ResponseLength.Detailed => "回复详细，提供完整的解释和步骤。",
            _ => "回复长度适中。"
        };
    }
}

/// <summary>
/// 语气风格
/// </summary>
public enum ToneStyle
{
    Friendly,
    Professional,
    Casual,
    Cute,
    Calm,
    Sarcastic
}

/// <summary>
/// 语言风格
/// </summary>
public enum LanguageStyle
{
    Standard,
    Concise,
    Detailed,
    Technical,
    Colloquial
}

/// <summary>
/// 回复长度偏好
/// </summary>
public enum ResponseLength
{
    Short,
    Balanced,
    Detailed
}
