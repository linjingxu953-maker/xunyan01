namespace DesktopMascot.Agent.Models;

/// <summary>
/// LLM 消息
/// </summary>
public class LlmMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    /// <summary>视觉内容（图片 URL 或 base64）</summary>
    public List<VisionContent>? Images { get; set; }
}

/// <summary>
/// 视觉内容
/// </summary>
public class VisionContent
{
    /// <summary>图片 URL</summary>
    public string? Url { get; set; }
    /// <summary>Base64 编码的图片数据</summary>
    public string? Base64Data { get; set; }
    /// <summary>图片类型（image/png, image/jpeg 等）</summary>
    public string MediaType { get; set; } = "image/png";
}

/// <summary>
/// LLM 响应
/// </summary>
public class LlmResponse
{
    public string Content { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    /// <summary>OpenAI 原生 tool_calls（由 Provider 解析填充）</summary>
    public List<ToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// 工具定义
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parameters { get; set; } = "{}";
}

/// <summary>
/// 工具调用
/// </summary>
public class ToolCall
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = "{}";
}

// ToolResult 已迁移至 Core.Tools.ToolModels，请使用 using DesktopMascot.Core.Tools;
