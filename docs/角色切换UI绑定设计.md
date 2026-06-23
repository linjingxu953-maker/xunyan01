# 角色切换 UI 绑定设计文档

> 目标：让 Codex 参照此文档实现角色切换的 UI 层绑定

## 一、Core/Agent 层已提供的接口

### ICharacterManager（Core 层）

| 方法 | 说明 | 返回值 |
|------|------|--------|
| `Current` | 当前活动角色 | `CharacterManifest?` |
| `IsReady` | 是否有角色加载 | `bool` |
| `Load(characterDirectory)` | 加载角色包 | `CharacterLoadResult` |
| `SwitchTo(slug)` | 按标识切换角色 | `bool` |
| `ListLoaded()` | 列出所有已加载角色 | `IReadOnlyList<CharacterSummary>` |
| `GetResourceReport(dir)` | 角色资源完整性报告 | `CharacterResourceReport` |
| `ImportFromPetdex(dir)` | 导入 Petdex 角色包 | `CharacterLoadResult` |
| `ResetToDefault()` | 恢复默认角色 | `void` |

### CharacterSummary（角色列表项）

```csharp
public class CharacterSummary
{
    public string Slug { get; }      // "yan", "boba" 等标识
    public string Name { get; }      // 显示名称
    public string Version { get; }   // 版本号
    public bool IsCurrent { get; }   // 是否当前角色
}
```

### CharacterManifest（角色完整信息）

```
Schema / Name / Slug / Version
├── Profile（人格）
│   ├── Role, Description, Personality
│   ├── ToneStyle, LanguageStyle, ReplyLength
│   ├── Catchphrase, Traits, UseEmoji
│   └── SystemPromptSuffix
├── Appearance（外观）
│   ├── AvatarText（头像文字）
│   ├── ImageFolder（图片目录）
│   ├── AvatarImage（头像图片）
│   ├── AccentColor / BackgroundColor
├── States（状态映射）
│   └── 每个状态：DisplayName / Image / FallbackState / PetdexState
├── Animation（动画配置）
│   ├── PrimaryMode（"state-images" 或 "lottie"）
│   ├── LottieAnimations
│   └── PetdexCompatibility
└── Metadata（元数据）
    ├── Author / License / Tags
    └── Market（商店信息）
```

### AgentPersonality（人格模型，Agent 层）

由 `CharacterToPersonalityConverter.Convert(manifest)` 从 CharacterManifest 生成。

| 属性 | 说明 |
|------|------|
| `Name` | 角色名称 |
| `Description` | 角色描述 |
| `Tone` | 语气风格（Friendly/Professional/Casual/Cute/Calm/Sarcastic） |
| `Language` | 语言风格（Standard/Concise/Detailed/Technical/Colloquial） |
| `Traits` | 性格特征列表 |
| `Catchphrase` | 口头禅 |
| `LengthPreference` | 回复长度偏好 |
| `UseEmoji` | 是否使用 emoji |

### CharacterSwitchTool（AI 可调用工具）

操作：`list` / `switch` / `import` / `current` / `resource_report`

---

## 二、UI 层需要实现的组件

### 1. 角色选择面板（CharacterSelectPanel）

**位置**：可放在设置页面内，或右键菜单中

**布局建议**：
- 左侧：角色列表（竖排卡片，每个显示头像文字 + 名称 + 版本）
- 右侧：当前角色详情（Profile + Appearance 预览）
- 底部：操作按钮（切换 / 导入 / 扫描）

**数据绑定**：
```
角色列表 ← ICharacterManager.ListLoaded()
当前角色 ← ICharacterManager.Current
切换操作 ← ICharacterManager.SwitchTo(slug)
```

### 2. 角色切换通知

当用户切换角色时，UI 需要更新：

| 组件 | 更新内容 |
|------|---------|
| 对话框标题 | 显示新角色名称 |
| 对话框主题色 | 使用新角色的 AccentColor |
| 角色图片 | 根据当前状态加载对应图片 |
| 对话框背景 | 使用新角色的 BackgroundColor |
| 发送按钮 | 使用新角色的 AccentColor |

### 3. 角色状态图片映射

当 Agent 处于不同状态时，UI 显示不同图片：

| Agent 状态 | 角色状态 | UI 行为 |
|-----------|---------|--------|
| 空闲 | Idle | 显示 Idle 图片（或默认头像） |
| 工作中 | Working | 显示 Working 图片 |
| 完成 | Completed | 显示 Completed 图片 |
| 错误 | Error | 显示 Error 图片 |
| 等待确认 | WaitingApproval | 显示 WaitingApproval 图片 |

**缺图回退**：`CharacterManager.ResolveStateImage(state, baseDir)` 自动回退到 Idle。

### 4. 角色资源报告（可选）

在设置页面显示角色资源完整性：

```
角色：Boba Cat
├── 头像：存在
├── Idle 图片：存在
├── Working 图片：缺失（回退到 Idle）
└── Petdex 精灵图：不存在
```

---

## 三、ViewModel 层设计建议

### CharacterSelectViewModel

```
属性：
├── Characters: ObservableCollection<CharacterSummary>
├── CurrentCharacter: CharacterManifest?
├── SelectedCharacter: CharacterSummary?
├── ResourceReport: CharacterResourceReport?
├── IsLoading: bool

方法：
├── LoadCharacters() — 调用 ListLoaded() 刷新列表
├── SwitchCharacter(slug) — 调用 SwitchTo()，成功后更新 CurrentCharacter
├── ImportCharacter(dir) — 调用 ImportFromPetdex()
├── RefreshReport() — 调用 GetResourceReport()
├── ResetToDefault() — 调用 ResetToDefault()
```

### 与 AgentOrchestrator 的联动

切换角色后，需要通知 Agent 层更新人格。有两种方式：

**方式 A（推荐）：通过 DI 容器**
- CharacterManager.SwitchTo() 后，CharacterSwitchTool 内部已调用 AgentOrchestrator.UpdatePersonality()
- UI 层只需调用 SwitchTo()，无需直接操作 Agent

**方式 B：事件通知**
- CharacterManager 暴发 CharacterChanged 事件
- AgentOrchestrator 订阅该事件，自动更新人格
- 需要额外的事件总线或回调机制

---

## 四、默认角色目录

```
%AppData%/DesktopMascot/characters/
├── feng-lin-yu-ren/  ← 默认角色（枫林渔人）
│   ├── character.json
│   ├── assets/
│   │   ├── avatar.png
│   │   ├── idle.png
│   │   ├── working.png
│   │   ├── completed.png
│   │   ├── error.png
│   │   └── waiting.png
├── yan/              ← 旧首角色（微风）
│   ├── character.json
│   └── assets/
├── boba/             ← 导入的 Petdex 角色
│   ├── character.json
│   └── assets/
└── custom/           ← 用户自定义角色
    ├── character.json
    └── assets/
```

---

## 五、注意事项

1. **MiMo 不改 UI 层**：此文档仅为 Codex 提供接口说明，所有 View/ViewModel/AXAML 修改由 Codex 执行
2. **角色切换是实时的**：切换后下一次对话立即使用新角色的语气/风格
3. **默认角色保护**：ResetToDefault() 恢复到第一个加载的角色，不会丢失
4. **缺图回退**：缺少某个状态的图片时自动回退到 Idle，不会崩溃
5. **线程安全**：CharacterManager 内部有锁，UI 线程可安全调用
