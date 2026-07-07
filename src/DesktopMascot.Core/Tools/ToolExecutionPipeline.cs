using System.Diagnostics;
using DesktopMascot.Core.Enums;
using DesktopMascot.Core.Interfaces;
using DesktopMascot.Core.Models;
using DesktopMascot.Core.Security;

namespace DesktopMascot.Core.Tools;

/// <summary>
/// 工具执行管道 - 集成权限检查、确认、执行、日志、事件
/// </summary>
public class ToolExecutionPipeline
{
    private readonly IToolRegistry _toolRegistry;
    private readonly PermissionConfirmationService _permissionService;
    private readonly ITaskEventStream? _eventStream;

    public ToolExecutionPipeline(
        IToolRegistry toolRegistry,
        PermissionConfirmationService permissionService,
        ITaskEventStream? eventStream = null)
    {
        _toolRegistry = toolRegistry;
        _permissionService = permissionService;
        _eventStream = eventStream;
    }

    /// <summary>
    /// 执行工具（带权限检查）
    /// </summary>
    public async Task<ToolCallResponse> ExecuteAsync(
        ToolCallRequest request,
        CancellationToken ct = default)
    {
        var taskId = request.TaskId ?? "";

        // 1. 检查工具是否存在
        var tool = _toolRegistry.GetTool(request.ToolName);
        if (tool == null)
        {
            return new ToolCallResponse
            {
                RequestId = request.RequestId ?? "",
                ToolName = request.ToolName,
                Success = false,
                Error = $"工具不存在: {request.ToolName}"
            };
        }

        // 2. 检查工具是否启用
        if (!tool.Definition.IsEnabled)
        {
            return new ToolCallResponse
            {
                RequestId = request.RequestId ?? "",
                ToolName = request.ToolName,
                Success = false,
                Error = $"工具已禁用: {request.ToolName}"
            };
        }

        // 3. 检查是否需要权限
        var requiredPermission = ToolPermissionPolicy.ResolveRequiredPermission(tool);
        if (requiredPermission > PermissionLevel.L0_Chat)
        {
            // 发布工具调用开始事件
            _eventStream?.Publish(TaskEvent.ToolCallStarted(
                taskId,
                request.ToolName,
                request.Arguments));

            // 请求权限确认
            var permissionType = ToolPermissionPolicy.ResolvePromptPermissionType(tool, requiredPermission);
            var permissionResponse = await _permissionService.RequestPermissionAsync(
                taskId,
                permissionType,
                request.ToolName,
                $"执行工具 {request.ToolName}",
                request.Arguments,
                ToolPermissionPolicy.ResolveRiskLevel(requiredPermission),
                ct);

            // 检查权限决策
            if (permissionResponse.Decision == PermissionDecision.Deny)
            {
                _eventStream?.Publish(TaskEvent.ToolCallCompleted(
                    taskId,
                    request.ToolName,
                    false,
                    "用户拒绝了权限"));

                return new ToolCallResponse
                {
                    RequestId = request.RequestId ?? "",
                    ToolName = request.ToolName,
                    Success = false,
                    Error = "用户拒绝了权限"
                };
            }
        }

        // 4. 执行工具
        var stopwatch = Stopwatch.StartNew();
        ToolCallResponse response;

        try
        {
            var result = await tool.ExecuteAsync(request.Arguments, ct);
            response = new ToolCallResponse
            {
                RequestId = request.RequestId ?? "",
                ToolName = request.ToolName,
                Success = result.Success,
                Result = result.Content,
                Error = result.Error,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            response = new ToolCallResponse
            {
                RequestId = request.RequestId ?? "",
                ToolName = request.ToolName,
                Success = false,
                Error = $"工具执行异常: {ex.Message}"
            };
        }

        stopwatch.Stop();
        response.Duration = stopwatch.Elapsed;

        // 5. 发布工具调用完成事件
        _eventStream?.Publish(TaskEvent.ToolCallCompleted(
            taskId,
            request.ToolName,
            response.Success,
            response.Success ? response.Result : response.Error));

        return response;
    }

    /// <summary>
    /// 映射到 PermissionType
    /// </summary>
    private static PromptPermissionType MapToPermissionType(PermissionLevel level)
    {
        return level switch
        {
            PermissionLevel.L1_WindowTitle => PromptPermissionType.WindowRead,
            PermissionLevel.L2_ScreenBrowser => PromptPermissionType.ScreenCapture,
            PermissionLevel.L3_FileRead => PromptPermissionType.FileRead,
            PermissionLevel.L4_FileWrite => PromptPermissionType.FileWrite,
            PermissionLevel.L5_CommandExec => PromptPermissionType.CommandExecute,
            PermissionLevel.L6_Forbidden => PromptPermissionType.CommandExecute,
            _ => PromptPermissionType.FileRead
        };
    }

    /// <summary>
    /// 映射到 RiskLevel
    /// </summary>
    private static PromptRiskLevel MapToRiskLevel(PermissionLevel level)
    {
        return level switch
        {
            PermissionLevel.L1_WindowTitle => PromptRiskLevel.Low,
            PermissionLevel.L2_ScreenBrowser => PromptRiskLevel.Low,
            PermissionLevel.L3_FileRead => PromptRiskLevel.Low,
            PermissionLevel.L4_FileWrite => PromptRiskLevel.Medium,
            PermissionLevel.L5_CommandExec => PromptRiskLevel.High,
            PermissionLevel.L6_Forbidden => PromptRiskLevel.High,
            _ => PromptRiskLevel.Low
        };
    }
}
