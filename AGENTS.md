# 桌面AI交互小人 - 项目记忆

本文件为项目通用记忆，供所有 agent（MiMo、Claude、Codex 等）共享访问。

---

## 项目基本信息

- **项目名称**：桌面AI交互小人（DesktopMascot）
- **项目位置**：`C:\Users\wgmo\Desktop\桌面交互ai小人`
- **技术栈**：C# / Avalonia UI 12.x + .NET 8
- **平台**：Windows 10/11

## 项目愿景

基于 MiMo Code、Hermes 等智能体的**功能有机整合**（非直接搬运），构建一个**通用型个人智能助理**：

**核心能力**：
- 屏幕识别理解（视觉 AI + 区域圈选）
- 多任务处理：大学生作业、刷网课、做文件、查资料、分析数据、编程、做设计、剪视频等
- 可视化人物角色外观，支持替换以满足不同用户审美
- 自我进化：从用户反馈学习、技能自动生成、记忆积累

**技术整合方向**：
- MiMo Code 的能力：代码辅助、文件编辑、终端命令、多步骤任务
- Hermes 的能力：记忆系统、技能生成、跨会话学习、上下文感知
- 视觉 AI：屏幕截图理解、区域圈选识别、OCR
- 动态角色：可替换的 2D 角色外观，支持个性化定制

**变现方向（后话）**：免费基础版 + Pro 订阅 + 角色/技能市场

---

## 设计决策

| 决策项 | 选择 | 理由 |
|--------|------|------|
| 桌面框架 | Avalonia UI | 跨平台潜力，Windows支持好 |
| 运行时 | .NET 8 | 稳定版本，Avalonia 12.x 要求 |
| UI模式 | MVVM | 社区工具链成熟 |
| MVVM工具 | CommunityToolkit.Mvvm | 源码生成器，减少模板代码 |
| 日志 | Serilog | 结构化日志，文件输出 |
| 测试 | xUnit | .NET 生态主流 |
| **屏幕理解** | **截图 + 视觉 LLM** | **零依赖，支持任意屏幕内容** |
| **区域圈选** | **全屏透明遮罩** | **体验类似微信截图，快捷键触发** |
| **视觉理解 prompt** | **三层：识别→理解→行动** | **未理解时返回用户输入意图，不瞎猜** |

---

## 目录结构约定

```
桌面交互ai小人/
├── src/                          # 源代码
│   ├── DesktopMascot.App/        # 主应用入口
│   ├── DesktopMascot.Core/       # 核心业务逻辑（无UI依赖）
│   ├── DesktopMascot.UI/         # Avalonia UI层
│   └── DesktopMascot.Agent/      # Agent/AI集成层
├── tests/                        # 测试项目
├── docs/                         # 项目文档
├── assets/                       # 资源文件（动画、图标）
├── mimo/                         # MiMo agent 工作产物
└── AGENTS.md                     # 本文件 - 通用记忆
```

---

## 任务分配

| 模块 | 负责 Agent | 说明 |
|------|-----------|------|
| UI（Views、ViewModels、AXAML） | Codex | 界面设计和交互实现 |
| Core（状态机、任务流、测试） | MiMo | 业务逻辑和单元测试 |
| Agent（AI集成） | MiMo | Agent 引擎和工具调用 |
| App 桥接服务 | MiMo | IContextBridgeService 等 |
| **屏幕理解 UI** | **Codex** | **全屏透明遮罩 + 鼠标拖拽画框** |
| **屏幕理解 Agent** | **MiMo** | **区域截图 + 视觉 LLM 理解 + 意图识别** |

## 边界规则（2026-06-12）

**MiMo Code 可操作范围：**
- `src/DesktopMascot.Agent` - 上下文/工具/路由
- `src/DesktopMascot.Core` - 业务逻辑

**Codex 操作范围：**
- `src/DesktopMascot.UI` - 界面和 ViewModel

**App 层（两者都不动）：**
- `src/DesktopMascot.App/App.axaml.cs` - 服务注册
- `src/DesktopMascot.App/ServiceCollectionExtensions.cs` - DI 配置

**桥接服务：**
- UI 不直接引用 Agent 的 `IContextProvider`
- 通过 `IContextBridgeService` 获取上下文数据

**依赖许可：**
- MiMo Code: MIT 协议（可商用，需保留版权声明）
- 详见 `THIRD_PARTY_LICENSES.md`

---

## 编码规范

> **铁律：文档类型文件（计划、设计文档、调研报告、借鉴方案等）非必要不写入代码样例。用描述性文字说明要做什么，代码实现在实际创建文件时才写。** 原因：agent 读文档时会把代码示例直接复制进实现文件，造成重复代码或不可用代码。

1. **命名**：PascalCase 类名，camelCase 字段，_前缀私有字段
2. **文件组织**：一个类一个文件，按功能分文件夹
3. **依赖方向**：App → UI → Core，Agent → Core（UI和Agent不直接依赖）
4. **接口优先**：跨层通信必须通过接口

## 测试运行规则（2026-06-17）

**所有 agent 跑解决方案全量测试时，必须使用安全脚本：**

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-full-tests-safe.ps1
```

**禁止直接裸跑：**

```powershell
dotnet test DesktopMascot.sln
```

原因：
- Avalonia BuildServices 会写入 `%LOCALAPPDATA%\AvaloniaUI\BuildServices\buildtasks.log`，在受限环境下可能触发 `UnauthorizedAccessException`，安全脚本会设置 `AVALONIA_TELEMETRY_OPTOUT=1`。
- 视频/外部进程类测试一旦卡住，裸跑不会给出清晰定位；安全脚本固定开启 `--blame-hang --blame-hang-timeout 90s --blame-hang-dump-type none`。
- 安全脚本默认使用带时间戳的 `TestResults\artifacts-safe-full-*` 输出目录，避免被遗留 `testhost` 锁住默认 `bin/obj` 或上一次 artifacts。

如果只跑模块级测试，可以直接跑对应测试项目；但涉及 Agent 视频处理、Computer Use、外部进程或解决方案级全量时，必须带 `--blame-hang` 或使用上述安全脚本。

---

## 当前进度

### M1: 可编译壳子 ✅ 已完成
### M2: 状态机闭环 ✅ 已完成

- [x] 修复 UI 问题（聊天面板入口、裁切、拖动干扰）
- [x] 删除模板 Class1.cs
- [x] TaskRouter 单元测试
- [x] TaskEventBus 单元测试
- [x] AgentEngineStub 单元测试
- [x] MascotStateMachine 状态机实现
- [x] 状态转换规则定义
- [x] 任务取消支持
- [x] 状态机单元测试
- [x] 22 个测试全部通过

### M4: Agent 层搭建 ✅ 已完成

- [x] Agent 目录结构
- [x] LLM 模型定义
- [x] ILlmProvider 接口
- [x] OpenAiProvider 实现
- [x] ITool 接口
- [x] ToolRegistry 工具注册表
- [x] 内置工具（GetCurrentTime、Calculator）
- [x] AgentOrchestrator 编排器
- [x] Agent 单元测试（16 个）
- [x] 38 个测试全部通过

### M5: 上下文 MVP ✅ 已完成

- [x] IContextProvider 接口
- [x] WindowsContextProvider 实现
- [x] MockContextProvider 测试用
- [x] GetActiveWindowTool 工具
- [x] ReadFileTool 工具
- [x] 上下文单元测试（10 个）
- [x] 50 个测试全部通过

### M6: 权限与安全 ✅ 已完成

- [x] PermissionModels 权限模型
- [x] IPermissionManager 接口
- [x] PermissionManager 实现（永久/会话授权）
- [x] CommandRiskAssessor 命令风险评估
- [x] IAuditLogStore 审计日志接口
- [x] FileAuditLogStore 文件存储实现
- [x] 权限单元测试（14 个）
- [x] 67 个测试全部通过

### M7: 记忆系统 ✅ 已完成

- [x] MemoryModels 记忆模型
- [x] IMemoryStore 接口
- [x] FileMemoryStore 文件存储实现
- [x] IMemoryConfirmationHandler 确认接口
- [x] MemoryManager 记忆管理器
- [x] 记忆单元测试（13 个）
- [x] 80 个测试全部通过

### M8: 插件系统 ✅ 已完成

- [x] IPlugin 插件接口
- [x] PluginMetadata 插件元数据
- [x] PluginLoader 插件加载器
- [x] PluginRegistry 插件注册表
- [x] PluginBase 插件基类
- [x] IPluginTool 插件工具接口
- [x] QuotesPlugin 示例插件
- [x] WeatherPlugin 示例插件
- [x] 插件单元测试（17 个）
- [x] 97 个测试全部通过

### M9: 任务历史 ✅ 已完成

- [x] TaskHistoryModels 任务历史模型
- [x] ITaskHistoryStore 存储接口
- [x] FileTaskHistoryStore 文件存储实现
- [x] TaskHistoryManager 任务管理器
- [x] 任务历史单元测试（16 个）
- [x] 113 个测试全部通过

### M10: 数据层（SQLite）✅ 已完成

- [x] DatabaseContext 数据库上下文
- [x] DatabaseMigrator 迁移管理器
- [x] SqliteTaskHistoryStore SQLite 存储实现
- [x] 数据库表创建（Tasks, TaskEvents, ToolCalls, Memories, AuditLogs, Settings）
- [x] 迁移版本管理
- [x] 数据层单元测试（5 个）
- [x] 118 个测试全部通过

### M11: 配置管理 ✅ 已完成

- [x] ConfigurationModels 配置模型
- [x] IConfigurationManager 接口
- [x] FileConfigurationManager 文件存储实现
- [x] ConfigurationExtensions 验证扩展
- [x] 配置单元测试（10 个）
- [x] 128 个测试全部通过

### M12: 日志系统 ✅ 已完成

- [x] LogModels 日志模型
- [x] ILogStore 日志存储接口
- [x] FileLogStore 文件存储实现
- [x] ILogger 日志管理器接口
- [x] LogManager 日志管理器实现（缓冲、自动刷新）
- [x] 日志单元测试（9 个）
- [x] 137 个测试全部通过

### M14: 错误处理 ✅ 已完成

- [x] ErrorModels 错误模型
- [x] ErrorHandler 错误处理器
- [x] SafeExecutor 安全执行器（重试、错误恢复）
- [x] 错误处理单元测试（11 个）
- [x] 154 个测试全部通过

### M15: 工具注册表 ✅ 已完成

- [x] ToolModels 工具模型
- [x] ITool/IToolProvider/IToolRegistry 接口
- [x] ToolRegistry 注册表实现
- [x] ToolBase 工具基类
- [x] 内置工具（时间、计算器、名言、天气）
- [x] 工具注册表单元测试（18 个）
- [x] 172 个测试全部通过

### M16: 任务流编排 ✅ 已完成

- [x] WorkflowModels 工作流模型
- [x] IWorkflowEngine 工作流引擎接口
- [x] WorkflowEngine 工作流引擎实现
- [x] WorkflowBuilder 工作流构建器
- [x] WorkflowTemplates 工作流模板
- [x] 工作流单元测试（14 个）
- [x] 186 个测试全部通过

### M17: 任务流持久化 ✅ 已完成

- [x] IWorkflowStore 工作流存储接口
- [x] FileWorkflowStore 文件存储实现
- [x] PersistentWorkflowEngine 支持持久化的引擎
- [x] 检查点恢复（ResumeFromCheckpoint）
- [x] 工作流持久化单元测试（11 个）
- [x] 197 个测试全部通过

### M18: 工具链组合 ✅ 已完成

- [x] ToolChainModels 工具链模型
- [x] IToolChainExecutor 执行器接口
- [x] ToolChainExecutor 执行器实现（顺序/并行/条件）
- [x] ToolChainBuilder 工具链构建器
- [x] 条件表达式评估
- [x] 结果传递和变量映射
- [x] 工具链单元测试（14 个）
- [x] 211 个测试全部通过

### M19: 任务调度器 ✅ 已完成

- [x] ScheduleModels 调度模型
- [x] ITaskScheduler 调度器接口
- [x] AppTaskScheduler 调度器实现
- [x] 定时/间隔/Cron 调度
- [x] 任务暂停/恢复
- [x] 重试机制
- [x] 调度单元测试（13 个）
- [x] 224 个测试全部通过

### M20: 缓存系统 ✅ 已完成

- [x] CacheModels 缓存模型
- [x] ICache 缓存接口
- [x] MemoryCache 内存缓存实现
- [x] CachedService<T> 缓存装饰器
- [x] CacheExtensions 缓存扩展方法
- [x] 支持绝对/滑动/文件依赖过期
- [x] 缓存统计（命中率、大小）
- [x] 缓存单元测试（16 个）
- [x] 240 个测试全部通过

### M31: 总结当前网页 ✅ 已完成

- [x] LlmMessage 支持视觉输入（Images 字段）
- [x] OpenAiCompatibleProvider 支持发送 base64 图片
- [x] BrowserContextTool 返回真实数据（窗口标题+截图路径）
- [x] AgentOrchestrator 支持 SummarizePage 专用路径
- [x] 视觉 LLM prompt 工程
- [x] 单元测试（10 个）
- [x] 250 个测试全部通过

### M32: 屏幕理解 🔄 Agent层完成，等UI

**目标**：用户圈选屏幕任意区域 → AI 识别理解 → 帮用户解决问题

**分层**：
1. **识别层**：这是什么内容（报错、代码、表格、图标...）
2. **理解层**：用户可能想做什么；**未理解时返回用户输入意图，不瞎猜**
3. **行动层**：给出建议或执行操作

**Agent 层（MiMo 负责）**：
- [x] ScreenUnderstandTool — 区域截图 + 视觉 LLM 理解
- [x] ScreenUnderstandPrompt — 三层 prompt（识别→理解→行动）
- [x] ScreenUnderstandResult — 结构化返回模型
- [x] AgentOrchestrator 集成 ScreenUnderstand 路径
- [x] 单元测试

**UI 层（Codex 负责）**：
- [ ] ScreenOverlay — Avalonia 全屏透明遮罩窗口
- [ ] 鼠标拖拽画框交互
- [ ] 框选完成回调 → 截取区域坐标
- [ ] ScreenSelectViewModel — 管理圈选状态
- [ ] 快捷键 Ctrl+Shift+S 触发

**快捷键**：Ctrl+Shift+S 触发屏幕理解

### M33: 场景专用路径+prompt ✅ 已完成

- [x] AnalyzeError 专用路径 + 报错分析 prompt
- [x] InspectProject 专用路径 + 项目诊断 prompt
- [x] SolveProblem 专用路径 + 题目解答 prompt（新增 TaskType）
- [x] ListDirectoryTool — 目录树遍历工具
- [x] ExecuteWithSpecializedPromptAsync — 共用的专用 prompt 执行器
- [x] 单元测试（7 个）
- [x] 261 个测试全部通过

### M34: Chat/WriteFile/RunCommand 专用 prompt ✅ 已完成

- [x] Chat 专用 prompt — 友善专业助手，直接回答
- [x] WriteFile 专用 prompt — 生成完整文件内容
- [x] RunCommand 专用 prompt — 生成和解释命令
- [x] AgentOrchestrator 路由表完整覆盖所有 TaskType
- [x] 单元测试（3 个）
- [x] 264 个测试全部通过

### M35: 端到端集成测试 ✅ 已完成

- [x] EndToEndTests — 验证 8 个场景的完整流程
- [x] AllTaskTypes_ShouldHaveDedicatedPrompt — 验证所有 TaskType 都有专用 prompt
- [x] 273 个测试全部通过

### M36: 记忆系统集成 ✅ 已完成

**目标**：模仿 Hermes 机制，让 AI 能够了解用户并自我增强进化

**核心能力**：
1. **执行前记忆检索** — 搜索用户偏好、项目信息、相关技能、历史经验，注入 prompt 上下文
2. **执行后记忆提议** — 分析任务结果，自动提议保存用户偏好、项目信息、任务历史
3. **Skill 自动生成** — 检测重复模式（同类任务 ≥3 次），自动生成可复用技能
4. **用户学习** — 通过历史积累了解用户习惯和偏好

**实现文件**：
- [x] `MemoryIntegrationService.cs` — 记忆集成服务核心
- [x] `MemoryContext` — 记忆上下文（注入 prompt）
- [x] `MemoryProposal` — 记忆提议模型
- [x] AgentOrchestrator 集成记忆检索+提议
- [x] 单元测试（8 个）
- [x] 281 个测试全部通过

**架构**：
```
AgentOrchestrator.ExecuteAsync()
  ├── SearchRelevantMemoriesAsync() → 注入 prompt
  ├── ExecuteWithSpecializedPromptAsync() → 执行任务
  └── ProposeMemoriesAsync() → 保存记忆
```

### M37: WriteFileTool + RunCommandTool + 权限确认 ✅ 已完成

- [x] WriteFileTool — 文件写入工具，支持创建和覆盖
- [x] RunCommandTool — 命令执行工具，支持超时和危险命令检测
- [x] ITool.RequiresConfirmation — 工具确认接口
- [x] ToolRegistry.RequiresConfirmation() — 检查确认需求
- [x] AgentOrchestrator 集成 PermissionRequested 事件
- [x] 单元测试（9 个）
- [x] 294 个测试全部通过

### M38: EditFileTool + SearchFileTool ✅ 已完成

- [x] EditFileTool — 文件编辑工具（替换/插入/删除/追加/前置）
- [x] SearchFileTool — 文件搜索工具（按文件名/内容/扩展名）
- [x] 单元测试（11 个）
- [x] 305 个测试全部通过

### M39: 浏览器正文提取 + 流式响应 ✅ 已完成

- [x] HtmlContentExtractor — HTML 正文提取器
- [x] 流式响应支持 — OpenAiCompatibleProvider SSE 解析
- [x] ExecuteStreamingAsync — AgentOrchestrator 流式执行
- [x] LlmStreamChunk 事件 — 实时推送 LLM 输出
- [x] 单元测试（7 个）
- [x] 319 个测试全部通过

### M40: Computer Use（Phase 2）✅ 已完成

**架构决策**：
- MiMo 负责 Agent/Core：截图、窗口识别、鼠标键盘动作、浏览器/桌面操作、动作规划、工具执行管道
- Codex 继续只做 UI：状态展示、权限确认、用户接管/中断、操作轨迹、结果反馈
- 模型调用走用户自己的 Provider/API Key，不内置 Key
- 所有敏感动作必须经过权限确认体系

**实现文件**：
- [x] `ComputerUseTool.cs` — 鼠标点击/键盘输入/窗口操作工具
- [x] `ComputerUseEvent.cs` — Computer Use 事件模型（9 种事件类型）
- [x] `ComputerUseOrchestrator.cs` — Computer Use 编排器（规划→执行→反馈）
- [x] UI 接口：ComputerUseStarted/ScreenObserved/ActionPlanned/ActionExecuting/ActionCompleted/WaitingUserApproval/UserTakeoverRequested/ComputerUseCompleted/ComputerUseFailed
- [x] 单元测试（7 个）
- [x] 319 个测试全部通过

### M41: ComputerUseOrchestrator 集成 ✅ 已完成

- [x] TaskType.ComputerUse 枚举
- [x] AgentOrchestrator.ExecuteComputerUseAsync() 专用路径
- [x] ConfiguredAgentEngine 集成 ComputerUseOrchestrator
- [x] DI 注册 ComputerUseOrchestrator
- [x] CaptureScreenAsync 异常处理（测试环境稳定性）
- [x] 集成测试（2 个）
- [x] 321 个测试全部通过

### M42: 流式响应 + 任务历史 + 边界测试 ✅ 已完成

- [x] IAgentEngine.ExecuteStreamingAsync() 接口
- [x] AgentOrchestrator.ExecuteStreamingAsync() 实现
- [x] ConfiguredAgentEngine 流式转发
- [x] MiMoCodeAgent 流式支持
- [x] AgentEngineStub 流式实现
- [x] AgentOrchestrator 自动记录任务历史
- [x] OperationCanceledException 处理
- [x] 边界测试 + 流式测试（10 个）
- [x] 331 个测试全部通过

### 下一阶段

- [ ] M3: 桌面体验（托盘、快捷键）- 等待 Codex 完成 UI
- [x] M22: 服务整合层 - DI容器、启动流程、服务协调器 ✅
- [x] M26: 状态事件流 - 完整事件结构定义 + UI 推送 ✅
- [x] M29: 权限确认接口 - IPermissionPrompt 接口定义 ✅
- [x] M30: 记忆确认接口 - IMemoryConfirmationPrompt 接口定义 ✅
- [x] M25: 工具执行管道 - 权限检查→确认→执行→结果返回 ✅
- [x] M24: TaskRouter 集成 - EnhancedTaskRouter 集成 TaskEventStream + ToolExecutionPipeline ✅
- [x] M23: LLM Provider 接入 - OpenAiCompatibleProvider + LlmProviderFactory + IApiKeyStore + 10+ Provider 支持 ✅
- [x] M27: WindowsContextProvider 完善 - 屏幕截图、窗口识别、剪贴板读取 ✅
- [x] M28: 工具实现 - ScreenCaptureTool、BrowserContextTool、ClipboardTool、ToolRegistryInitializer ✅

#### 等待中

- [ ] M32: 屏幕理解 UI - 等待 Codex 完成全屏遮罩 + 圈选交互
- [ ] Computer Use UI - 等待 Codex 完成控制面板壳子
- [ ] M31+: 封测包 - 等用户验收功能后再规划

#### 已完成

- [x] WriteFileTool - 文件写入 ✅
- [x] EditFileTool - 文件编辑 ✅
- [x] SearchFileTool - 文件搜索 ✅
- [x] RunCommandTool - 命令执行 ✅
- [x] ComputerUseTool - 鼠标键盘控制 ✅
- [x] ComputerUseOrchestrator - 动作规划执行 ✅
- [x] 权限确认机制 - PermissionRequested 事件 ✅
- [x] HTML 正文提取 - HtmlContentExtractor ✅
- [x] 流式响应 - ChatStreamAsync + ExecuteStreamingAsync ✅
- [x] 记忆系统集成 - MemoryIntegrationService ✅

---

## 已知问题与注意事项

1. **Avalonia 12 API 变化**：
   - `SystemDecorations` 改为 `WindowDecorations`
   - `AllowsTransparency` 和 `ExtendClientAreaChromeHints` 已移除
   - 使用 `TransparencyLevelHint="AcrylicBlur"` 替代
2. **命名冲突**：`TaskStatus` 与 `System.Threading.Tasks.TaskStatus` 冲突，已重命名为 `AppTaskStatus`
3. **.NET SDK**：已通过 winget 安装 8.0.422
4. **Avalonia Templates**：已安装 12.0.4
5. **Bug 审计（2026-06-12）**：Claude 全项目审计，2 严重 + 5 中等 + 6 建议。详见 `docs/2026-06-12-项目当前问题记录.md`
   - 🔴 严重：Timer async void 崩溃风险（TaskScheduler.cs:65）、WorkflowEngine 审批步骤自动绕过（WorkflowEngine.cs:153-158）
   - 🟡 中等：FloatingWindowViewModel 950 行需拆分（UI/Codex 负责）、FileMemoryStore O(n) 性能、PermissionManager 未注册 DI、ResolveArguments null ref、API Key 明文
6. **API 接入设计（2026-06-12）**：用户自行配置 API Key，支持多 Provider（含国产模型：DeepSeek、Kimi、智谱、百川、讯飞、通义、豆包、零一万物、MiniMax、阶跃星辰）

---

## 环境配置

- **.NET SDK**：8.0.422
- **Avalonia Templates**：12.0.4
- **VS Code 扩展**：Avalonia for VS Code

---

## 项目统计

| 指标 | 数值 |
|------|------|
| 已完成模块 | 55+ 个（M1-M2, M4-M20, M22-M28, M31-M42, 快速迭代22项, 角色包3项） |
| 单元测试 | 558 个全部通过（Agent 315 + Core 232 + UI 11） |
| 代码行数 | 约 20000+ 行 |
| 核心模块 | Core, Agent, App |
| 内置工具 | 32 个（全部注册到 ToolRegistry） |
| 待完成 | M3（UI）、M32 UI、Computer Use UI、封测包 |

## 更新日志

| 日期 | 更新内容 |
|------|----------|
| 2026-06-16-17 | 视频处理工具 + HyperFrames 集成（VideoProcessingTool + ShortVideoMakerTool） |
| 2026-06-16-17 | Petdex 调研 + 角色包格式（CharacterModels + CharacterPackageLoader + PetdexImportConverter） |
| 2026-06-16-17 | DI 注册补全（LearningEngine + ConversationManager + AgentPersonality + EdgeTts + CharacterPackage） |
| 2026-06-16-17 | IntentClassifier 增强（+26 个意图模式覆盖全部工具） |
| 2026-06-16-17 | FileMemoryStore O(1) 优化（Dictionary 索引 + 30秒批量刷新） |
| 2026-06-16-17 | 角色包集成测试（7 个端到端测试） |
| 2026-06-16-17 | Bug 确认（Timer async void 已修复、WorkflowEngine 审批绕过已修复） |
| 2026-06-16-17 | 安全测试脚本 run-full-tests-safe.ps1 + 538→558 测试 |
| 2026-06-11 | M5 完成：上下文 MVP |
| 2026-06-11 | M6 完成：权限与安全 |
| 2026-06-11 | M7 完成：记忆系统 |
| 2026-06-11 | M8 完成：插件系统 |
| 2026-06-11 | M9 完成：任务历史 |
| 2026-06-11 | M10 完成：数据层（SQLite） |
| 2026-06-11 | M11 完成：配置管理 |
| 2026-06-11 | M12 完成：日志系统 |
| 2026-06-11 | M14 完成：错误处理 |
| 2026-06-11 | M15 完成：工具注册表 |
| 2026-06-11 | M16 完成：任务流编排 |
| 2026-06-11 | M17 完成：任务流持久化 |
| 2026-06-11 | M18 完成：工具链组合 |
| 2026-06-11 | M19 完成：任务调度器 |
| 2026-06-11 | M20 完成：缓存系统 |
| 2026-06-12 | MiMo 任务规划：M22-M30 功能模块，封测包等用户验收后再做 |
| 2026-06-12 | API 接入设计：用户自行配置 Key，支持国产模型（DeepSeek、Kimi 等 10+），创建 docs/API接入设计.md |
| 2026-06-12 | M22 完成：服务整合层（DI 容器、启动流程、服务协调器）|
| 2026-06-12 | M26 完成：状态事件流（TaskEventType 枚举、TaskEvent 工厂方法、ITaskEventStream 接口、TaskEventStream 实现）|
| 2026-06-12 | M29 完成：权限确认接口（IPermissionPrompt、PermissionPromptRequest/Response、DefaultPermissionPrompt、PermissionConfirmationService）|
| 2026-06-12 | M30 完成：记忆确认接口（IMemoryConfirmationPrompt、MemoryConfirmationRequest/Response、MemoryDecision、DefaultMemoryConfirmationPrompt、MemoryConfirmationService）|
| 2026-06-12 | M25 完成：工具执行管道（ToolExecutionPipeline - 权限检查、确认、执行、日志、事件）|
| 2026-06-12 | M24 完成：TaskRouter 集成（EnhancedTaskRouter - 集成 TaskEventStream + ToolExecutionPipeline）|
| 2026-06-12 | M23 完成：LLM Provider 接入（OpenAiCompatibleProvider + LlmProviderFactory + IApiKeyStore + 10+ Provider 支持）|
| 2026-06-12 | M27 完成：WindowsContextProvider 完善（屏幕截图、窗口识别、剪贴板读取）|
| 2026-06-12 | M28 完成：工具实现（ScreenCaptureTool、BrowserContextTool、ClipboardTool、ToolRegistryInitializer）|
| 2026-06-12 | 创建 MiMoCodeAgent（通过 CLI 调用 MiMo Code）|
| 2026-06-12 | 创建 IContextBridgeService（App 层桥接服务，隔离 UI 与 Agent 依赖）|
| 2026-06-12 | 明确边界规则：MiMo 操作 Agent/Core，Codex 操作 UI，App 层不动|
| 2026-06-12 | 创建 THIRD_PARTY_LICENSES.md（记录 MiMo Code MIT 许可证）|
| 2026-06-12 | 创建 docs/状态事件与确认接口设计.md（M26/M29/M30 接口定义）|
| 2026-06-14 | M31 完成：总结当前网页（视觉 LLM + SummarizePage 专用路径 + 测试）|
| 2026-06-14 | M32 Agent层完成：ScreenUnderstandTool + 三层 prompt + 意图 fallback + AgentOrchestrator 路由 + 测试 |
| 2026-06-14 | M33 完成：报错分析/项目诊断/题目解答专用路径+prompt + ListDirectoryTool |
| 2026-06-14 | M34 完成：Chat/WriteFile/RunCommand 专用 prompt + 路由表完整覆盖 |
| 2026-06-14 | M35 完成：端到端集成测试（9 个场景 + AllTaskTypes 验证）|
| 2026-06-14 | M36 完成：记忆系统集成（MemoryIntegrationService + 上下文注入 + 记忆提议 + Skill 自动生成）|
| 2026-06-14 | M37 完成：WriteFileTool + RunCommandTool + 权限确认机制 |
| 2026-06-14 | M38 完成：EditFileTool + SearchFileTool |
| 2026-06-14 | M39 完成：HtmlContentExtractor + 流式响应（SSE + ExecuteStreamingAsync）|
| 2026-06-14 | M40 完成：Computer Use Phase 2（ComputerUseTool + ComputerUseEvent + ComputerUseOrchestrator）|
| 2026-06-14 | 架构决策：MiMo 负责 Agent/Core，Codex 负责 UI（控制面板）|
| 2026-06-14 | M41 完成：ComputerUseOrchestrator 集成（TaskType.ComputerUse + AgentOrchestrator 路由 + DI 注册）|
| 2026-06-14 | M42 完成：任务历史持久化 + OperationCanceledException 处理 + 7 个边界测试 |

---

## 设计文档

- `docs/API接入设计.md` — API 接入完整设计（Provider 列表、配置流程、接口、安全存储、国产模型支持）
- `docs/状态事件与确认接口设计.md` — 状态事件结构 + 权限/记忆确认接口定义（M26/M29/M30）
- `docs/MiMo-Petdex角色格式借鉴边界方案.md` — 角色包格式借鉴边界（自有格式 + Petdex 可选兼容层）
- `Petdex借鉴方案.md`（桌面） — Petdex 项目调研 + 可借鉴内容
