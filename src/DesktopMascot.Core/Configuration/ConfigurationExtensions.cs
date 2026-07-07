namespace DesktopMascot.Core.Configuration;

public static class ConfigurationExtensions
{
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

    public static List<string> Validate(this AppSettings settings)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.ModelName))
            errors.Add("模型名称不能为空");

        if (string.IsNullOrWhiteSpace(settings.ApiEndpoint))
            errors.Add("API 端点不能为空");

        if (settings.WindowScale <= 0 || settings.WindowScale > 2)
            errors.Add("窗口缩放比例必须在 0-2 之间");

        return errors;
    }

    public static List<string> Validate(this UserPreferences preferences)
    {
        var errors = new List<string>();

        if (preferences.AutoSaveInterval < 0)
            errors.Add("自动保存间隔不能为负数");

        return errors;
    }

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
