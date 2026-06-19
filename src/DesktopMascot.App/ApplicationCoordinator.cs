using DesktopMascot.Core.Character;
using DesktopMascot.Core.Configuration;
using DesktopMascot.Core.Logging;
using DesktopMascot.Core.Memory;
using DesktopMascot.Core.Plugins;
using DesktopMascot.Core.Scheduling;
using DesktopMascot.Core.Security;
using DesktopMascot.Core.Services;
using DesktopMascot.Core.Storage;
using DesktopMascot.Core.Tools;
using DesktopMascot.Core.Workflow;
using Microsoft.Extensions.DependencyInjection;
using ILogger = DesktopMascot.Core.Logging.ILogger;

namespace DesktopMascot.App;

/// <summary>
/// 应用服务协调器 - 统一管理服务生命周期
/// </summary>
public class ApplicationCoordinator : IAsyncDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private bool _isInitialized;
    private bool _isDisposed;

    /// <summary>应用状态</summary>
    public ApplicationState State { get; private set; } = ApplicationState.Uninitialized;

    /// <summary>状态变更事件</summary>
    public event EventHandler<ApplicationStateEventArgs>? StateChanged;

    public ApplicationCoordinator(IServiceProvider services, ILogger logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// 初始化应用服务（按依赖顺序）
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized)
            return;

        try
        {
            _logger.Information("开始初始化应用服务");
            SetState(ApplicationState.Initializing);

            // 1. 加载配置
            await InitializeConfigurationAsync(ct);

            // 2. 初始化日志
            await InitializeLoggingAsync(ct);

            // 3. 初始化存储层
            await InitializeStorageAsync(ct);

            // 4. 初始化内存系统
            await InitializeMemoryAsync(ct);

            // 5. 初始化权限系统
            await InitializePermissionAsync(ct);

            // 6. 初始化工具注册表
            await InitializeToolsAsync(ct);

            // 7. 初始化角色管理
            await InitializeCharactersAsync(ct);

            // 8. 初始化工作流引擎
            await InitializeWorkflowAsync(ct);

            // 9. 初始化调度器
            await InitializeSchedulerAsync(ct);

            // 10. 初始化插件
            await InitializePluginsAsync(ct);

            _isInitialized = true;
            SetState(ApplicationState.Running);
            _logger.Information("应用服务初始化完成");
        }
        catch (Exception ex)
        {
            _logger.Error($"应用初始化失败: {ex.Message}", exception: ex);
            SetState(ApplicationState.Error);
            throw;
        }
    }

    /// <summary>
    /// 停止应用服务
    /// </summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        if (!_isInitialized || _isDisposed)
            return;

        try
        {
            _logger.Information("开始停止应用服务");
            SetState(ApplicationState.Stopping);

            // 按依赖逆序停止
            await StopPluginsAsync(ct);
            await StopSchedulerAsync(ct);
            await FlushLogsAsync(ct);

            SetState(ApplicationState.Stopped);
            _logger.Information("应用服务已停止");
        }
        catch (Exception ex)
        {
            _logger.Error($"停止应用服务失败: {ex.Message}", exception: ex);
            SetState(ApplicationState.Error);
        }
    }

    private async Task InitializeConfigurationAsync(CancellationToken ct)
    {
        _logger.Debug("初始化配置管理器");
        var config = _services.GetRequiredService<IConfigurationManager>();
        var settings = await config.GetAppSettingsAsync(ct);
        _logger.Debug($"配置加载完成，数据目录: {settings.DataDirectory}");
    }

    private async Task InitializeLoggingAsync(CancellationToken ct)
    {
        _logger.Debug("初始化日志系统");
        var logger = _services.GetRequiredService<Core.Logging.ILogger>();
        logger.Information("日志系统初始化完成");
        await Task.CompletedTask;
    }

    private async Task InitializeStorageAsync(CancellationToken ct)
    {
        _logger.Debug("初始化存储层");
        var taskHistory = _services.GetRequiredService<ITaskHistoryStore>();
        var stats = await taskHistory.GetStatisticsAsync(ct);
        _logger.Debug($"任务历史存储初始化完成，共 {stats.TotalTasks} 条记录");
    }

    private async Task InitializeMemoryAsync(CancellationToken ct)
    {
        _logger.Debug("初始化记忆系统");
        var memory = _services.GetRequiredService<IMemoryStore>();
        var stats = await memory.GetStatisticsAsync(ct);
        _logger.Debug($"记忆系统初始化完成，共 {stats.TotalCount} 条记忆");

        // 启动时自动备份 + 清理过期记忆
        try
        {
            var persistence = _services.GetService<MemoryPersistenceManager>();
            if (persistence != null)
            {
                var backupResult = await persistence.BackupAsync("启动时自动备份", ct);
                _logger.Debug($"自动备份完成：{backupResult.EntryCount} 条记忆");

                var cleanupResult = await persistence.CleanupAsync(maxAgeDays: 90, onlyUnconfirmed: true, ct);
                if (cleanupResult.RemovedCount > 0)
                    _logger.Debug($"清理过期记忆：{cleanupResult.RemovedCount} 条");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"记忆持久化操作失败（不影响启动）：{ex.Message}");
        }
    }

    private async Task InitializePermissionAsync(CancellationToken ct)
    {
        _logger.Debug("初始化权限系统");
        var permission = _services.GetRequiredService<IPermissionManager>();
        var logs = await permission.GetAuditLogsAsync(10, ct);
        _logger.Debug($"权限系统初始化完成，最近 {logs.Count} 条审计记录");
    }

    private async Task InitializeToolsAsync(CancellationToken ct)
    {
        _logger.Debug("初始化工具注册表");
        var toolRegistry = _services.GetRequiredService<Core.Tools.IToolRegistry>();
        var tools = toolRegistry.GetAllDefinitions().ToList();
        _logger.Debug($"工具注册表初始化完成，共 {tools.Count} 个工具");
        await Task.CompletedTask;
    }

    private async Task InitializeCharactersAsync(CancellationToken ct)
    {
        _logger.Debug("初始化角色管理");
        var characterManager = _services.GetRequiredService<ICharacterManager>();

        // 扫描默认角色目录
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopMascot", "characters");

        if (Directory.Exists(dataDir))
        {
            var dirs = Directory.GetDirectories(dataDir);
            var loadedCount = 0;

            foreach (var dir in dirs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var result = characterManager.Load(dir);
                    if (result.Success)
                        loadedCount++;
                }
                catch (Exception ex)
                {
                    _logger.Warning($"加载角色 {Path.GetFileName(dir)} 失败: {ex.Message}");
                }
            }

            _logger.Debug($"角色初始化完成，加载 {loadedCount} 个角色");
        }
        else
        {
            Directory.CreateDirectory(dataDir);
            _logger.Debug("角色目录不存在，已创建默认目录");
        }

        // 如果有已加载角色，记录当前角色
        if (characterManager.IsReady)
        {
            var current = characterManager.Current!;
            _logger.Debug($"当前角色：{current.Name}（{current.Slug}）");
        }
        else
        {
            _logger.Debug("暂无已加载角色");
        }

        await Task.CompletedTask;
    }

    private async Task InitializeWorkflowAsync(CancellationToken ct)
    {
        _logger.Debug("初始化工作流引擎");
        var workflow = _services.GetRequiredService<IWorkflowEngine>();
        _logger.Debug("工作流引擎初始化完成");
        await Task.CompletedTask;
    }

    private async Task InitializeSchedulerAsync(CancellationToken ct)
    {
        _logger.Debug("初始化任务调度器");
        var scheduler = _services.GetRequiredService<ITaskScheduler>();
        await scheduler.StartAsync(ct);
        _logger.Debug("任务调度器初始化完成");
    }

    private async Task InitializePluginsAsync(CancellationToken ct)
    {
        _logger.Debug("初始化插件系统");
        var pluginRegistry = _services.GetRequiredService<PluginRegistry>();
        await pluginRegistry.LoadAllPluginsAsync(ct);
        _logger.Debug($"插件系统初始化完成，共 {pluginRegistry.Plugins.Count} 个插件");
    }

    private async Task StopPluginsAsync(CancellationToken ct)
    {
        _logger.Debug("停止插件系统");
        var pluginRegistry = _services.GetRequiredService<PluginRegistry>();
        foreach (var plugin in pluginRegistry.Plugins.Values.ToList())
        {
            try
            {
                await plugin.DisableAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.Warning($"禁用插件 {plugin.Metadata.Id} 失败: {ex.Message}");
            }
        }
    }

    private async Task StopSchedulerAsync(CancellationToken ct)
    {
        _logger.Debug("停止任务调度器");
        var scheduler = _services.GetRequiredService<ITaskScheduler>();
        await scheduler.StopAsync(ct);
    }

    private async Task FlushLogsAsync(CancellationToken ct)
    {
        _logger.Debug("刷新日志缓冲区");
        var logger = _services.GetRequiredService<Core.Logging.ILogger>();
        await logger.FlushAsync(ct);
    }

    private void SetState(ApplicationState state)
    {
        var oldState = State;
        State = state;
        StateChanged?.Invoke(this, new ApplicationStateEventArgs(oldState, state));
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        await StopAsync();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 应用状态枚举
/// </summary>
public enum ApplicationState
{
    Uninitialized,
    Initializing,
    Running,
    Stopping,
    Stopped,
    Error
}

/// <summary>
/// 应用状态事件参数
/// </summary>
public class ApplicationStateEventArgs : EventArgs
{
    public ApplicationState OldState { get; }
    public ApplicationState NewState { get; }

    public ApplicationStateEventArgs(ApplicationState oldState, ApplicationState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}
