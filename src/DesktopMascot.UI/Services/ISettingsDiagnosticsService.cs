namespace DesktopMascot.UI.Services;

public sealed record ModelConnectionTestRequest(
    string ProviderName,
    string ApiEndpoint,
    string ModelName,
    string ApiKey);

public sealed record MimoCodeConnectionTestRequest(
    bool IsEnabled,
    string ExecutablePath,
    string WorkspacePath,
    string ModelConfigMode,
    string ProviderName,
    string ApiEndpoint,
    string ModelName,
    string ApiKey);

public sealed record SettingsDiagnosticsResult(
    bool Success,
    string Message,
    string Detail);

public interface ISettingsDiagnosticsService
{
    Task<SettingsDiagnosticsResult> TestModelConnectionAsync(
        ModelConnectionTestRequest request,
        CancellationToken ct = default);

    Task<SettingsDiagnosticsResult> TestMimoCodeAsync(
        MimoCodeConnectionTestRequest request,
        CancellationToken ct = default);
}
