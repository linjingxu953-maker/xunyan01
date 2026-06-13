using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 获取当前时间工具
/// </summary>
public class GetCurrentTimeTool : ITool
{
    public string Name => "get_current_time";
    public string Description => "获取当前日期和时间";
    public string ParametersSchema => "{}";

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        return Task.FromResult(new ToolResult
        {
            Name = Name,
            Success = true,
            Content = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }
}

/// <summary>
/// 计算器工具
/// </summary>
public class CalculatorTool : ITool
{
    public string Name => "calculator";
    public string Description => "执行数学计算";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "expression": {
                "type": "string",
                "description": "数学表达式"
            }
        },
        "required": ["expression"]
    }
    """;

    public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var expression = doc.RootElement.GetProperty("expression").GetString() ?? "";

            // 简化的计算器（生产环境应使用更安全的解析器）
            var result = EvaluateExpression(expression);

            return Task.FromResult(new ToolResult
            {
                Name = Name,
                Success = true,
                Content = result.ToString()
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult
            {
                Name = Name,
                Success = false,
                Error = $"计算错误: {ex.Message}"
            });
        }
    }

    private static double EvaluateExpression(string expression)
    {
        // 简化实现：只支持基本四则运算
        // 生产环境应使用 DataTable.Compute 或第三方库
        var dt = new System.Data.DataTable();
        var result = dt.Compute(expression, "");
        return Convert.ToDouble(result);
    }
}
