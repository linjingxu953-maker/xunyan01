# Petdex 借鉴方案

> 来源：https://github.com/crafter-station/petdex (3k⭐)
> 目标：将 Petdex 可借鉴部分适配到桌面AI小人项目

---

## 一、可借鉴内容总览

| 类别 | 内容 | 借鉴程度 | 改造量 |
|------|------|---------|--------|
| 精灵图格式 | 8×9网格、192×208帧、72帧/状态 | 直接可用 | 无需改造 |
| 动画状态枚举 | idle/wave/run/failed/review/jump | 直接可用 | 无需改造 |
| Agent活动映射 | Agent事件→动画状态的对应关系 | 需适配 | 事件名映射 |
| 角色元数据格式 | pet.json 结构 | 直接可用 | 字段扩展 |
| 状态机触发逻辑 | Agent钩子→状态切换 | 需适配 | JS→C# |
| 精灵图播放器 | Canvas/WebGL播放 | 需适配 | 渲染层不同 |
| 社区提交流程 | 标准化格式+审核机制 | 参考设计 | 流程简化 |

---

## 二、精灵图格式规范

### Petdex 格式

- 精灵图尺寸：1920×1872 像素
- 网格布局：8行 × 9列
- 单帧尺寸：192×208 像素
- 总帧数：72帧
- 动画循环：每状态6帧，循环播放
- 默认帧率：1100ms/6帧 ≈ 183ms/帧

### 动画状态行对应

| 行号 | 状态 | 用途 |
|------|------|------|
| 0 | idle | 空闲/待机 |
| 1 | wave | 成功/打招呼 |
| 2 | run | 执行任务/工作中 |
| 3 | failed | 执行失败/错误 |
| 4 | review | 等待审查/权限确认 |
| 5 | jump | 启动/初始化 |
| 6 | extra1 | 扩展状态1 |
| 7 | extra2 | 扩展状态2 |

### 我们如何使用

保留 Petdex 的状态枚举，但动画用 Lottie JSON 定义（支持更丰富动画效果），同时兼容 Petdex 精灵图格式以便用户从 petdex.dev 下载宠物直接使用。

---

## 三、动画状态枚举（直接复用）

直接复用 Petdex 的 8 种状态定义：Idle、Wave、Run、Failed、Review、Jump、Extra1、Extra2。

### 与现有 MascotStateMachine 的映射

| Petdex 状态 | 我们的 AppTaskStatus | 触发时机 |
|------------|---------------------|---------|
| Idle | — | 无任务执行时 |
| Wave | Completed | 任务成功完成 |
| Run | InProgress | 任务执行中 |
| Failed | Failed | 任务执行失败 |
| Review | WaitingPermission | 需要用户确认 |
| Jump | Created | 任务刚创建 |

---

## 四、Agent 活动映射表

### Petdex 的映射逻辑

- Agent 执行代码 → run 状态（工作中）
- Agent 完成任务 → wave 状态（成功）
- Agent 执行失败 → failed 状态（错误）
- Agent 等待审批 → review 状态（审查中）
- Agent 空闲 → idle 状态（待机）
- Agent 启动 → jump 状态（初始化）

### 我们的实现要点

在 AgentOrchestrator 中，根据 TaskEvent 的 EventType 切换角色状态。映射规则：TaskStarted→Run、TaskResultReady→Wave、TaskFailed→Failed、PermissionRequested→Review、TaskCreated→Jump、无事件→Idle。

---

## 五、角色元数据格式

### Petdex 的 pet.json 结构

必填字段：name、slug、kind、frameSize（width/height）、grid（rows/cols）、animationStates（每个状态含 row、frames、loopMs）。

可选字段：tags、vibes、description、author。

### 我们扩展后的字段

在 Petdex 基础上增加：

- version：角色版本号
- author：作者信息
- license：许可证
- lottieAnimations：Lottie 动画文件路径映射（idle/working/success/error 等）
- customStates：自定义状态定义（listening/thinking/typing 等）

### 目录结构

```
assets/characters/boba/
├── pet.json              # 角色元数据（兼容Petdex）
├── spritesheet.webp      # 精灵图（兼容Petdex）
└── animations/           # Lottie动画（我们扩展）
    ├── idle.json
    ├── working.json
    ├── success.json
    └── error.json
```

---

## 六、精灵图播放器适配

### Petdex 的播放逻辑

从精灵图中按状态行号和帧序号裁剪出单帧，定时切换帧实现动画播放。

### 我们的 Avalonia 适配要点

使用 DispatcherTimer 定时切换帧，从 Bitmap 中按行列坐标裁剪子区域显示。支持两种播放模式：精灵图模式（兼容 Petdex 格式）和 Lottie 模式（使用现有 Lottie 播放器）。

---

## 七、状态机触发集成

### 接入点：AgentOrchestrator

任务开始时通知 Run 状态，任务成功通知 Wave 状态，任务失败通知 Failed 状态，取消时恢复 Idle 状态。

### 接入点：PermissionManager

权限确认请求时通知 Review 状态，用户确认后恢复 Run 状态。

### 接入点：TaskEventBus

订阅 TaskEventStream，将 TaskEventType 自动映射为 MascotState 并通知 UI 层。

---

## 八、角色市场设计（参考 Petdex 提交流程）

### Petdex 的提交流程

1. 用户创建宠物文件夹（pet.json + spritesheet）
2. `npx petdex submit` 提交
3. 管理员审核
4. 上架到 petdex.dev

### 我们的简化流程

Phase 1（v1）：用户制作角色放入目录 → 自动识别 → 即时可用
Phase 2（后续）：角色商店网站 → 社区分享 + 审核

### 角色商店元数据

包含 storeId、name、author、description、preview、downloads、rating、tags、compatible、license、downloadUrl。

---

## 九、实现优先级

### Phase 1：格式兼容

- 定义角色配置文件格式（扩展 pet.json）
- 支持加载 Petdex 精灵图格式
- 实现精灵图播放器（Avalonia 版）
- 添加 MascotState 枚举（复用 Petdex 定义）

### Phase 2：状态集成

- AgentOrchestrator 集成角色状态通知
- PermissionManager 集成审查状态
- TaskEventBus → MascotState 映射
- 状态切换动画过渡

### Phase 3：角色管理

- 角色选择界面（UI层，Codex负责）
- 角色目录扫描和加载
- 内置默认角色
- 自定义角色导入

### Phase 4：角色市场

- 角色商店网站
- 社区提交和审核
- 评分和推荐系统

---

## 十、关键差异

| 方面 | Petdex | 我们 |
|------|--------|------|
| 角色用途 | 纯展示/装饰 | 交互界面（理解屏幕、执行任务） |
| 状态来源 | Agent钩子（JS） | TaskEventBus（C#） |
| 动画格式 | 精灵图（固定网格） | Lottie + 精灵图（双格式支持） |
| 平台 | macOS优先 | Windows优先 |
| 技术栈 | Zig/JS/Canvas | C#/Avalonia |
| 商业模式 | 开源免费 | 免费+Pro订阅+角色市场 |

---

*文档生成时间：2026-06-16*
*参考项目：https://github.com/crafter-station/petdex*
