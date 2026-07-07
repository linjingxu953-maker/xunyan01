# DesktopMascot - 桌面AI交互小人

基于 AI Agent 的桌面智能助理，拥有可视化角色外观，支持屏幕理解、多任务处理、记忆进化等能力。

## 功能特性

### 核心能力
- **屏幕理解** — 圈选屏幕任意区域，AI 识别内容并给出建议（Ctrl+Shift+S）
- **多任务处理** — 编程、写文件、执行命令、分析数据、翻译、写论文等 36 种内置工具
- **视觉角色** — 可替换的 2D 角色外观，支持个性化定制
- **记忆系统** — 从用户反馈学习，技能自动生成，跨会话记忆积累
- **流式响应** — 实时输出 LLM 回答，不阻塞

### AI 集成
- 支持 10+ LLM Provider（OpenAI、DeepSeek、Kimi、智谱、通义等）
- Computer Use — 自动操控鼠标键盘完成桌面任务
- 权限确认机制 — 敏感操作需用户审批
- 插件系统 — 可扩展自定义工具

## 技术栈

| 组件 | 技术 |
|------|------|
| UI 框架 | Avalonia UI 12.x |
| 运行时 | .NET 8 |
| 架构模式 | MVVM |
| 测试框架 | xUnit |
| 日志 | Serilog |

## 项目结构

```
DesktopMascot/
├── src/
│   ├── DesktopMascot.App/        # 主应用入口
│   ├── DesktopMascot.Core/       # 核心业务逻辑
│   ├── DesktopMascot.UI/         # Avalonia UI 层
│   └── DesktopMascot.Agent/      # Agent/AI 集成层
├── tests/                        # 单元测试（740+）
├── assets/                       # 角色资源文件
└── docs/                         # 设计文档
```

## 快速开始

### 环境要求
- .NET SDK 8.0+
- Windows 10/11

### 编译运行

```bash
dotnet build DesktopMascot.sln
dotnet run --project src/DesktopMascot.App
```

### 运行测试

```powershell
powershell -ExecutionPolicy Bypass -File scripts/run-full-tests-safe.ps1
```

## API 配置

首次运行需配置 LLM Provider 的 API Key：

1. 打开设置面板
2. 选择 Provider（DeepSeek、OpenAI、Kimi 等）
3. 输入 API Key
4. 保存配置

详见 [docs/API接入设计.md](docs/API接入设计.md)

## 内置工具（36 种）

| 类别 | 工具 |
|------|------|
| 文件操作 | 读取、写入、编辑、搜索文件 |
| 系统命令 | 执行终端命令 |
| 屏幕理解 | 区域截图 + 视觉 LLM 分析 |
| 浏览器 | 自动化操作浏览器 |
| 图像处理 | 截图、压缩、格式转换 |
| 安全扫描 | 代码安全审计、漏洞检测 |
| 加密解密 | 文件加密（PBKDF2） |
| 网络请求 | HTTP 请求、API 调用 |
| 翻译 | 多语言翻译 |
| 论文写作 | 学术论文辅助 |
| 代码分析 | 静态分析、复杂度评估 |
| 更多... | 通知、日历、缓存、工作流... |

## 开发指南

### 边界规则
- **MiMo 负责**: Agent 层（工具、编排器）、Core 层（业务逻辑、测试）
- **Codex 负责**: UI 层（Views、ViewModels、AXAML）

### 编码规范
- PascalCase 类名，camelCase 字段，_前缀私有字段
- 一个类一个文件，按功能分文件夹
- 跨层通信通过接口

## 项目统计

| 指标 | 数值 |
|------|------|
| 源代码文件 | 314 个 |
| 单元测试 | 740+ |
| 代码行数 | 25,000+ |
| 内置工具 | 36 种 |
| Git 提交 | 70+ |

## 许可证

[MIT License](LICENSE)

## 致谢

- [Avalonia UI](https://avaloniaui.net/) — 跨平台 UI 框架
- [MiMo Code](https://github.com/mimo-ai/cli) — Agent 引擎（MIT 许可）
- [DeepSeek](https://deepseek.com/) — LLM Provider
