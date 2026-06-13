using System.Collections.Generic;

namespace DesktopMascot.UI.ViewModels;

public static class ModelProviderCatalog
{
    public static IReadOnlyList<ModelProviderOption> CreateDefaults() =>
    [
        new ModelProviderOption("OpenAI", "OpenAI", "https://api.openai.com/v1", "gpt-4o-mini"),
        new ModelProviderOption("DeepSeek", "DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
        new ModelProviderOption("Kimi", "Kimi（月之暗面）", "https://api.moonshot.cn/v1", "moonshot-v1-8k"),
        new ModelProviderOption("Zhipu", "智谱 AI", "https://open.bigmodel.cn/api/paas/v4", "glm-4-flash"),
        new ModelProviderOption("Baichuan", "百川智能", "https://api.baichuan-ai.com/v1", "Baichuan4"),
        new ModelProviderOption("Xunfei", "讯飞星火", "https://spark-api-open.xf-yun.com/v1", "spark-lite"),
        new ModelProviderOption("Tongyi", "通义千问", "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-plus"),
        new ModelProviderOption("Doubao", "豆包", "https://ark.cn-beijing.volces.com/api/v3", "doubao-pro-32k"),
        new ModelProviderOption("Yi", "零一万物", "https://api.lingyiwanwu.com/v1", "yi-lightning"),
        new ModelProviderOption("MiniMax", "MiniMax", "https://api.minimax.chat/v1", "abab6.5s-chat"),
        new ModelProviderOption("StepFun", "阶跃星辰", "https://api.stepfun.com/v1", "step-1-8k"),
        new ModelProviderOption("Custom", "自定义 OpenAI Compatible", "https://example.com/v1", "model-name")
    ];
}
