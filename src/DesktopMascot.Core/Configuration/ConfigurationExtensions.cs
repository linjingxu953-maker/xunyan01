namespace DesktopMascot.Core.Configuration;

/// <summary>
/// 配置扩展方法
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// 获取配置值，如果不存在则返回默认值
    /// </summary>
    public static T GetOrDefault<T>(this IConfigurationManager manager, Func<Task<T>> getter, T defaultValue)
    {
        try
        {
            return getter().Result;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// 验证应用设置
    /// </summary>
    public static List<string> Validate(this AppSettings settings)
    {
        var errors = new List<string>();
        
        if (string.IsNullOrEmpty(settings.ApiKey))
            errors.Add("API Key 不能为空");
        
        if (string.IsNullOrEmpty(settings.ModelName))
            errors.Add("模型名称不能为空");
        
        if (string.IsNullOrEmpty(settings.ApiEndpoint))
            errors.Add("API 端点不能为空");
        
        if (settings.WindowScale <= 0 || settings.WindowScale > 2)
            errors.Add("窗口缩放比例必须在 0-2 之间");
        
        return errors;
    }

    /// <summary>
    /// 验证用户偏好
    /// </summary>
    public static List<string> Validate(this UserPreferences preferences)
    {
        var errors = new List<string>();
        
        if (preferences.AutoSaveInterval < 0)
            errors.Add("自动保存间隔不能为负数");
        
        return errors;
    }

    /// <summary>
    /// 验证权限设置
    /// </summary>
    public static List<string> Validate(this PermissionSettings settings)
    {
        var errors = new List<string>();
        
        if (settings.AutoApproveLevel < 0 || settings.AutoApproveLevel > 6)
            errors.Add("自动批准级别必须在 0-6 之间");
        
        if (settings.AuditLogRetentionDays < 0)
            errors.Add("审计日志保留天数不能为负数");
        
        return errors;
    }
}
