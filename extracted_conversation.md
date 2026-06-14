下面是你这个项目的 **初版完整方案**。我按“可以真实开发、可以商业封测、不是概念 Demo”的标准来写。

---

# 桌面交互式 AI 小人初版方案

## 0. 初版定位

产品定位：

> **一款 Windows 桌面常驻 AI 小人，能够读取当前屏幕、浏览器和本地文件，接收用户任务，进入多步骤工作流，并通过 2D 动画反馈任务状态，同时具备用户可控的长期记忆。**

初版不是做“会聊天的桌宠”，而是做：

```text
桌面小人形态
+ 屏幕上下文理解
+ 文件 / 浏览器读取
+ Hermes Agent 工作流大脑
+ 手动确认式长期记忆
+ 高危操作权限确认
+ 商业化可封测产品
```

核心目标：

```text
1. 用户看到一个桌面 AI 小人，而不是普通聊天窗口。
2. 用户可以直接对当前屏幕发任务。
3. AI 能进入工作流，而不是只回答一句话。
4. AI 在执行过程中有清晰状态动画。
5. 涉及文件写入、命令执行时必须确认。
6. 用户可以控制它记住什么、忘记什么。
```

Everywhere 适合在早期承担桌面上下文感知和工具接入验证，因为它本身定位为 context-aware desktop AI assistant，支持屏幕上下文、LLM、MCP 工具等能力；Hermes Agent 适合作为长期任务大脑，因为它强调持久记忆、自动技能生成、跨会话检索和任务自动化；OpenHuman 只做产品体验参考，因为它的桌面 mascot、Memory Tree、本地 Markdown 知识库等方向与本项目体验目标接近，但不建议直接复制代码进入商业产品。citeturn627872search0turn627872search1turn627872search5

---

# 1. 初版边界

## 1.1 平台边界

初版只做：

```text
Windows 10 / Windows 11
```

不做：

```text
macOS
Linux
手机端
浏览器插件独立版
微信 / QQ / 飞书机器人
```

原因：  
Windows 桌面权限、悬浮窗、文件访问、快捷键、托盘、安装包、开机自启这些能力已经足够复杂。初版要先把单平台产品打透。

---

## 1.2 交互边界

初版支持：

```text
1. 桌面悬浮小人
2. 文本输入
3. 快捷键唤起
4. 鼠标点击小人
5. 任务状态动画
6. 气泡式反馈
7. 简易任务面板
```

初版不做：

```text
1. 语音输入
2. 语音播报
3. 3D 小人
4. Live2D 高复杂骨骼动画
5. 换装系统
6. 多角色系统
7. 表情商店
```

动画技术选择：

```text
优先：Lottie
备用：PNG 序列帧
后期：Live2D
```

初版的动画目标不是精美，而是状态清晰。

---

## 1.3 能力边界

初版支持：

```text
1. 读取当前屏幕内容
2. 读取当前窗口标题和应用名
3. 读取选中文本
4. 读取浏览器当前页面
5. 读取用户授权的本地文件
6. 生成新文件
7. 调用 Hermes 进入工作流
8. 长期记忆，但必须手动确认
9. 执行命令前必须确认
```

初版不支持：

```text
1. 自动删除文件
2. 自动提交网页表单
3. 自动支付
4. 自动发送邮件
5. 自动发布内容
6. 自动操作账号
7. 无确认执行终端命令
8. 无确认修改用户文件
```

---

# 2. 产品形态

## 2.1 桌面主形态

用户看到的是一个小人，常驻桌面右下角或用户自定义位置。

基础界面包括：

```text
1. 小人主体
2. 对话气泡
3. 输入框
4. 任务进度小面板
5. 权限确认弹窗
6. 记忆确认弹窗
7. 设置页
8. 托盘菜单
```

小人默认状态：

```text
空闲时轻微呼吸 / 眨眼
用户唤起时看向用户
思考时进入思考动画
执行任务时进入工作动画
需要确认时举手提醒
完成时递交结果
失败时显示困惑状态
```

---

## 2.2 初版主界面结构

```text
桌面小人
  ├── 小人动画区
  ├── 简短气泡反馈
  ├── 输入框
  ├── 当前任务状态
  └── 展开按钮
        └── 任务详情面板
```

任务详情面板显示：

```text
任务名称
任务状态
当前步骤
进度百分比
正在调用的工具
是否等待确认
最终结果
任务历史
```

---

# 3. 核心用户场景

初版必须围绕 3 个高频场景做，不要泛化成“什么都能做”。

---

## 场景 1：当前网页总结

用户行为：

```text
用户打开一个网页
按快捷键唤起小人
输入：总结这个页面
```

系统流程：

```text
1. 小人进入 Listening 状态
2. 读取当前浏览器标题、URL、正文内容
3. 交给 Hermes 或普通 LLM 总结
4. 返回结构化摘要
5. 小人进入 Completed 状态
```

输出格式：

```text
1. 核心结论
2. 关键要点
3. 可执行建议
4. 是否保存到项目记忆
```

---

## 场景 2：当前报错分析

用户行为：

```text
用户在 VS Code / 终端 / 浏览器里看到报错
按快捷键唤起小人
输入：这个报错怎么解决？
```

系统流程：

```text
1. 读取当前窗口
2. 读取选中文本或屏幕内容
3. 判断是否为代码 / 终端 / 网页报错
4. Hermes 分析原因
5. 给出修复步骤
6. 如需执行命令，弹出确认
```

权限规则：

```text
只解释：不需要确认
读取项目文件：需要授权目录
执行 npm install / pip install / git 命令：必须确认
修改代码文件：必须确认
```

---

## 场景 3：项目目录诊断

用户行为：

```text
用户选择一个本地项目目录
输入：帮我分析这个项目现在还缺什么
```

系统流程：

```text
1. 用户授权项目目录
2. 小人读取 README、package.json、requirements.txt、src 结构等
3. Hermes 生成项目诊断
4. 输出问题清单、开发建议、下一步任务
5. 用户确认后保存为 Markdown 报告
6. 可选择写入项目记忆
```

输出内容：

```text
1. 项目概述
2. 当前结构分析
3. 已完成能力
4. 缺失能力
5. 风险点
6. 下一步开发清单
```

---

# 4. 系统架构

## 4.1 总体架构

```text
Windows Desktop Shell
  ↓
Mascot UI / Animation State Machine
  ↓
Task Router
  ↓
Context Adapter
  ├── Screen Context
  ├── Active Window Context
  ├── Selected Text Context
  ├── Browser Context
  └── File Context
  ↓
Hermes Adapter
  ↓
Hermes Agent Runtime
  ↓
Tool Permission Manager
  ↓
Memory Center
  ↓
Local Storage / Logs
```

---

## 4.2 模块职责

| 模块 | 职责 | 初版是否必须 |
|---|---|---|
| Mascot Shell | 桌面悬浮小人、输入框、气泡、托盘 | 必须 |
| Animation State Machine | 根据任务事件切换动画 | 必须 |
| Task Router | 判断任务类型，分发给不同处理器 | 必须 |
| Context Adapter | 读取屏幕、窗口、浏览器、文件 | 必须 |
| Hermes Adapter | 对接 Hermes，执行工作流 | 必须 |
| Permission Manager | 控制读写文件、执行命令权限 | 必须 |
| Memory Center | 用户记忆、项目记忆、技能记忆 | 必须 |
| Task History | 保存任务记录和结果 | 必须 |
| Settings Center | 模型 Key、权限、记忆、启动项设置 | 必须 |
| Update System | 自动更新 | 初版可延后 |
| Plugin Market | 插件市场 | 不做 |

---

# 5. 技术选型

## 5.1 桌面端

建议：

```text
C# + Avalonia
```

理由：

```text
1. Windows 桌面能力成熟
2. 与 Everywhere 技术方向较接近
3. 适合做悬浮窗、托盘、设置页
4. 后期有跨平台可能
```

备选：

```text
Tauri + Rust + 前端
```

不建议初版使用 Electron，原因是：

```text
1. 体积偏大
2. 内存占用偏高
3. 桌面小人常驻运行，资源占用会被用户感知
```

---

## 5.2 Agent 层

建议：

```text
Hermes Agent Runtime
```

Hermes 适合做长期 Agent Runtime，因为它强调自我学习循环、持久记忆、自动技能生成、跨会话搜索、定时任务和多平台网关能力。它的文档也说明 memory 用于环境事实、工作流经验、项目约定、工具问题、任务日记等内容，skills 则更接近可复用流程记忆。citeturn627872search2turn627872search12turn627872search17

但商业产品中不能让 Hermes 直接面对用户。必须加一层：

```text
Hermes Adapter
```

作用：

```text
1. 统一任务输入格式
2. 转换 Hermes 事件为小人动画状态
3. 拦截危险工具调用
4. 做任务取消、超时、重试
5. 把可记忆内容交给 Memory Center
```

---

## 5.3 上下文感知层

初期：

```text
Everywhere Adapter
```

中期：

```text
自研 Context Adapter
```

原因：Everywhere 当前仓库页面显示其项目许可证为 Business Source License 1.1，因此商业产品需要认真处理授权边界。初版可以用 Everywhere 验证能力，但正式商业版本建议将上下文读取能力抽象成接口，后续逐步自研或取得授权。citeturn627872search0turn627872search20

---

## 5.4 本地存储

建议：

```text
SQLite + Markdown
```

SQLite 存：

```text
任务历史
权限记录
工具调用日志
模型配置
用户设置
记忆索引
```

Markdown 存：

```text
用户记忆
项目记忆
技能记忆
每日任务摘要
```

---

# 6. 初版目录结构

建议项目结构：

```text
desktop-ai-mascot/
  apps/
    desktop-shell/
      src/
      assets/
        animations/
        mascot/
      config/
    local-service/
      src/
      api/
      task-router/
      permission/
      memory/
      context/
      hermes/
  adapters/
    everywhere-adapter/
    browser-adapter/
    file-adapter/
  data/
    memory/
      user.md
      projects/
      skills/
      daily/
    db/
      app.sqlite
    logs/
  docs/
    product.md
    architecture.md
    state-machine.md
    permission-policy.md
    memory-policy.md
```

商业产品正式安装后，本地用户数据目录建议：

```text
C:/Users/{username}/AppData/Roaming/DesktopAIMascot/
  memory/
  db/
  logs/
  config/
  cache/
```

---

# 7. 状态机设计

## 7.1 初版状态

| 状态 | 含义 | 动画 |
|---|---|---|
| Idle | 空闲 | 呼吸、眨眼 |
| Listening | 接收任务 | 看向用户、轻微前倾 |
| Understanding | 理解用户意图 | 思考表情 |
| ReadingContext | 读取屏幕/文件/网页 | 看屏幕、翻资料 |
| Planning | 规划步骤 | 展开清单、点头 |
| WaitingApproval | 等待用户确认 | 举手、提示 |
| Working | 执行任务 | 敲键盘、忙碌 |
| MemoryConfirm | 请求保存记忆 | 拿出笔记本 |
| Reporting | 生成汇报 | 整理文件 |
| Completed | 完成 | 点头、递交结果 |
| Error | 出错 | 困惑、摇头 |

---

## 7.2 状态事件协议

```json
{
  "task_id": "task_20260610_001",
  "event_type": "state_changed",
  "state": "ReadingContext",
  "message": "正在读取当前浏览器页面",
  "progress": 25,
  "risk_level": "low",
  "timestamp": "2026-06-10T12:00:00"
}
```

---

## 7.3 需要确认的事件

```json
{
  "task_id": "task_20260610_002",
  "event_type": "approval_required",
  "state": "WaitingApproval",
  "message": "我需要执行 npm install 来安装依赖。",
  "risk_level": "high",
  "action": {
    "type": "terminal_command",
    "command": "npm install",
    "working_directory": "D:/Projects/demo"
  },
  "options": [
    "allow_once",
    "deny",
    "view_details"
  ]
}
```

---

# 8. Task Router 设计

Task Router 是系统中枢，负责判断用户任务应该走哪条链路。

## 8.1 任务类型

```text
ChatTask：普通问答
ScreenTask：当前屏幕任务
BrowserTask：网页总结 / 网页分析
FileTask：文件读取 / 文档生成
CodeTask：代码解释 / 报错修复
WorkflowTask：多步骤工作流
MemoryTask：记忆保存 / 记忆查询
SystemTask：设置、权限、启动项
```

---

## 8.2 路由规则

| 用户输入 | 路由 |
|---|---|
| “总结这个页面” | BrowserTask |
| “这个报错怎么解决” | ScreenTask + CodeTask |
| “帮我看这个文件” | FileTask |
| “帮我分析这个项目” | WorkflowTask |
| “以后都这样写” | MemoryTask |
| “帮我运行测试” | CodeTask + WaitingApproval |
| “你记住了什么” | MemoryTask |

---

## 8.3 任务生命周期

```text
Created
↓
ContextCollecting
↓
ContextReady
↓
Planning
↓
WaitingApproval / Running
↓
ToolCalling
↓
ResultGenerated
↓
MemoryConfirm
↓
Completed / Failed
```

---

# 9. Context Adapter 设计

## 9.1 初版上下文能力

| 能力 | 说明 |
|---|---|
| 当前窗口识别 | 应用名、窗口标题 |
| 当前屏幕读取 | 截图、屏幕摘要 |
| 选中文本读取 | 用户选中的文字 |
| 浏览器读取 | URL、标题、正文 |
| 文件读取 | 用户授权文件或目录 |

---

## 9.2 上下文格式

```json
{
  "context_id": "ctx_001",
  "source": "desktop",
  "active_app": "Microsoft Edge",
  "window_title": "Hermes Agent Documentation",
  "selected_text": "",
  "browser": {
    "url": "current_url",
    "title": "page_title",
    "content_summary": "页面摘要"
  },
  "screen": {
    "screenshot_path": "cache/screen_001.png",
    "screen_summary": "当前屏幕内容摘要"
  },
  "files": []
}
```

---

## 9.3 上下文读取原则

```text
1. 默认不长期保存屏幕截图
2. 屏幕截图只进入临时缓存
3. 浏览器正文只用于当前任务
4. 文件内容不自动进入长期记忆
5. 用户确认后才写入项目记忆
```

---

# 10. 权限系统设计

这是商业版底线。

## 10.1 权限等级

| 等级 | 操作 | 初版策略 |
|---|---|---|
| L0 | 普通问答 | 直接允许 |
| L1 | 读取当前窗口标题 | 首次提示 |
| L2 | 读取屏幕 / 选中文本 | 首次授权，可关闭 |
| L3 | 读取文件 / 目录 | 用户选择授权 |
| L4 | 新建文件 | 每次确认 |
| L5 | 覆盖 / 修改文件 | 强确认 |
| L6 | 执行终端命令 | 强确认 |
| L7 | 删除文件 / 批量操作 | 初版默认不支持 |

---

## 10.2 命令执行确认

示例：

```text
小人请求执行命令：

npm install

执行目录：
D:/Projects/DesktopAIPet

目的：
安装项目依赖，尝试修复当前缺失模块问题。

可能影响：
1. 联网下载依赖
2. 修改 node_modules
3. 修改 package-lock.json

是否允许？

[允许一次] [拒绝] [查看完整计划]
```

初版不允许：

```text
永久允许所有命令
静默执行命令
自动删除文件
自动提交 git push
自动安装未知程序
```

---

## 10.3 文件写入确认

示例：

```text
小人准备创建文件：

D:/Projects/report.md

内容：
项目诊断报告

是否允许写入？

[允许] [修改文件名] [拒绝]
```

覆盖文件时必须提示：

```text
该文件已存在，覆盖会替换原内容。
建议先创建副本。

[创建副本] [仍然覆盖] [取消]
```

---

# 11. 记忆系统设计

## 11.1 记忆原则

```text
1. 默认不自动记忆
2. 用户确认后才长期保存
3. 记忆必须可查看
4. 记忆必须可删除
5. 记忆必须可编辑
6. 外部网页内容不直接写入长期记忆
7. 文件内容不自动写入长期记忆
8. 敏感内容默认不保存
```

---

## 11.2 记忆分类

| 类型 | 内容 |
|---|---|
| 用户记忆 | 用户偏好、表达风格、常用工作方式 |
| 项目记忆 | 项目目标、架构、进度、约束 |
| 技能记忆 | 重复任务的流程，例如“网页总结模板” |
| 任务历史 | 每次任务的输入、输出、状态 |
| 工具经验 | 某些工具调用失败或成功经验 |

---

## 11.3 记忆文件结构

```text
memory/
  user.md
  projects/
    desktop_ai_mascot.md
  skills/
    webpage_summary.md
    code_error_fix.md
    project_diagnosis.md
  daily/
    2026-06-10.md
```

---

## 11.4 用户记忆示例

```markdown
# 用户记忆

## 偏好
- 用户希望回答直接、结构清晰。
- 用户做 PPT 时不希望使用任何形式的图标。
- 用户当前正在开发 Windows 桌面交互式 AI 小人商业产品。

## 产品约束
- Windows 优先。
- 第一版不做语音。
- 使用 2D Lottie / 序列帧动画。
- 长期记忆默认手动确认。
- 执行命令必须确认。
```

---

## 11.5 记忆确认弹窗

```text
我可以记住这条偏好：

“用户希望桌面 AI 小人第一版不做语音，优先使用 2D Lottie / 序列帧。”

是否保存到长期记忆？

[保存] [仅本次有效] [不保存]
```

---

# 12. 初版功能清单

## 12.1 必须做

```text
1. Windows 悬浮小人
2. 小人基础动画
3. 快捷键唤起
4. 文本输入框
5. 对话气泡
6. 任务详情面板
7. 当前窗口识别
8. 当前屏幕读取
9. 选中文本读取
10. 浏览器页面读取
11. 本地文件读取
12. Hermes 接入
13. Task Router
14. 状态事件流
15. 权限确认
16. 手动记忆确认
17. 任务历史
18. 设置页
19. 本地日志
20. 安装包
```

---

## 12.2 可以延后

```text
1. 自动更新
2. 多模型市场
3. 语音输入
4. 语音播报
5. Live2D
6. 多角色
7. 插件市场
8. 手机端同步
9. 团队空间
10. 企业管理后台
```

---

# 13. 初版页面设计

## 13.1 主界面

```text
小人主体
气泡提示
输入框
展开任务详情按钮
```

---

## 13.2 任务详情面板

包含：

```text
任务标题
当前状态
执行步骤
进度条
工具调用记录
最终结果
保存结果按钮
中断任务按钮
```

---

## 13.3 设置页

包含：

```text
模型设置
API Key 设置
权限设置
记忆设置
数据目录
开机自启
快捷键设置
日志导出
关于产品
```

---

## 13.4 记忆中心

包含：

```text
用户记忆
项目记忆
技能记忆
任务历史
搜索记忆
删除记忆
编辑记忆
导出记忆
```

---

## 13.5 权限中心

包含：

```text
已授权目录
屏幕读取权限
浏览器读取权限
文件读取权限
命令执行记录
撤销授权
```

---

# 14. 初版 API 设计

## 14.1 创建任务

```http
POST /api/tasks
```

请求：

```json
{
  "input": "总结这个页面",
  "source": "desktop",
  "context_required": true
}
```

返回：

```json
{
  "task_id": "task_001",
  "status": "created"
}
```

---

## 14.2 状态流

```http
GET /api/tasks/{task_id}/events
```

或 WebSocket：

```text
ws://localhost:port/events
```

事件：

```json
{
  "task_id": "task_001",
  "state": "Working",
  "message": "正在分析页面内容",
  "progress": 60
}
```

---

## 14.3 权限确认

```http
POST /api/permissions/approve
```

请求：

```json
{
  "task_id": "task_001",
  "approval_id": "approval_001",
  "decision": "allow_once"
}
```

---

## 14.4 保存记忆

```http
POST /api/memory/save
```

请求：

```json
{
  "type": "user_preference",
  "content": "用户希望 PPT 优化时不要使用图标。",
  "confirmed": true
}
```

---

# 15. 数据库表设计

## 15.1 tasks

```text
id
title
input
task_type
status
created_at
updated_at
result_summary
```

---

## 15.2 task_events

```text
id
task_id
event_type
state
message
progress
created_at
```

---

## 15.3 tool_calls

```text
id
task_id
tool_name
input_summary
output_summary
risk_level
approved
success
created_at
```

---

## 15.4 permissions

```text
id
permission_type
scope
status
created_at
expires_at
```

---

## 15.5 memories

```text
id
memory_type
title
content
file_path
confirmed
created_at
updated_at
```

---

# 16. 开发排期

## 第 1 周：项目骨架

目标：跑通桌面小人 + 本地服务。

完成：

```text
1. 创建桌面端项目
2. 创建本地服务项目
3. 创建 SQLite 数据库
4. 创建 WebSocket 状态通道
5. 创建基础输入框
6. 创建 Idle / Thinking / Completed 动画
```

验收：

```text
用户输入一句话，小人能切换 Thinking，再显示假结果。
```

---

## 第 2 周：Hermes 接入

目标：让小人能真正调用 Agent。

完成：

```text
1. 安装并配置 Hermes
2. 编写 Hermes Adapter
3. 实现 send_task
4. 实现 stream_events
5. 实现 cancel_task
6. 把 Hermes 输出转成小人状态
```

验收：

```text
用户输入“帮我规划今天开发任务”，Hermes 返回结果，小人展示。
```

---

## 第 3 周：上下文读取

目标：让它区别于普通聊天框。

完成：

```text
1. 当前窗口识别
2. 选中文本读取
3. 当前屏幕截图
4. 浏览器标题 / URL 读取
5. Everywhere Adapter 初步验证
```

验收：

```text
用户打开网页，说“总结这个页面”，小人能基于当前页面输出摘要。
```

---

## 第 4 周：文件能力

目标：支持本地文件和项目目录。

完成：

```text
1. 文件选择器
2. 目录授权
3. 读取 txt / md / pdf / docx 基础内容
4. 项目目录结构扫描
5. 文件摘要
6. 写入 Markdown 文件前确认
```

验收：

```text
用户选择项目目录，小人生成项目诊断报告，并在确认后保存。
```

---

## 第 5 周：权限系统

目标：达到商业产品安全底线。

完成：

```text
1. 权限等级设计落地
2. 读取屏幕授权
3. 读取目录授权
4. 写文件确认
5. 执行命令确认
6. 工具调用日志
7. 权限中心页面
```

验收：

```text
任何写文件和执行命令动作都不能绕过确认。
```

---

## 第 6 周：记忆系统

目标：实现手动确认式长期记忆。

完成：

```text
1. Memory Center
2. user.md
3. project memory
4. skill memory
5. 记忆确认弹窗
6. 记忆查看
7. 记忆删除
8. 记忆编辑
```

验收：

```text
用户说“以后都这样”，小人请求确认，确认后写入长期记忆。
```

---

## 第 7 周：产品体验优化

目标：从技术 Demo 变成可试用产品。

完成：

```text
1. 新手引导
2. 设置页
3. 托盘菜单
4. 快捷键设置
5. 错误提示优化
6. 任务历史页面
7. 动画细节优化
```

验收：

```text
新用户安装后 3 分钟内能完成第一个任务。
```

---

## 第 8 周：打包封测

目标：做出可安装封测版。

完成：

```text
1. Windows 安装包
2. 开机自启
3. 本地配置保存
4. 日志导出
5. 崩溃恢复
6. 封测说明文档
7. 三个演示场景录屏
```

验收：

```text
普通用户可以安装、配置模型 Key、启动小人、完成网页总结/报错分析/项目诊断。
```

---

# 17. 初版验收标准

## 17.1 产品验收

```text
1. 小人可以常驻桌面
2. 小人可以被快捷键唤起
3. 小人可以接收文本任务
4. 小人可以读取当前屏幕上下文
5. 小人可以读取浏览器页面
6. 小人可以读取授权文件
7. 小人可以调用 Hermes 执行任务
8. 小人可以展示任务状态动画
9. 小人可以请求权限确认
10. 小人可以手动保存长期记忆
```

---

## 17.2 安全验收

```text
1. 未授权不能读取任意目录
2. 未确认不能写文件
3. 未确认不能执行命令
4. 删除文件初版默认不支持
5. 屏幕截图不能默认长期保存
6. 外部网页不能自动写入长期记忆
7. 用户可以删除记忆
8. 用户可以撤销权限
```

---

## 17.3 商业封测验收

```text
1. 安装流程不依赖命令行
2. 用户能配置模型 Key
3. 软件能稳定运行 2 小时以上
4. 小人动画不卡顿
5. 任务失败时有明确提示
6. 日志能帮助定位问题
7. 三个核心场景能稳定演示
```

---

# 18. 初版商业包装

## 18.1 产品名称方向

可以考虑：

```text
桌面 AI 小人
桌面 Agent 小助手
TaskPet
WorkPet AI
AIPal Desktop
小灵工
桌面小灵
```

中文商业定位可以写：

> **一个能看懂你当前屏幕、帮你推进任务、并记住你工作习惯的桌面 AI 小人。**

---

## 18.2 首发卖点

```text
1. 屏幕感知：不用复制粘贴，直接理解当前窗口。
2. 工作流执行：能拆解任务、调用工具、生成结果。
3. 动画反馈：每一步执行状态都看得见。
4. 长期记忆：记住用户偏好和项目上下文。
5. 安全确认：写文件和执行命令前必须用户批准。
6. 本地优先：记忆和任务历史优先保存在本地。
```

---

## 18.3 首批目标用户

建议先打这几类：

```text
1. 程序员 / 独立开发者
2. 学生创赛 / 项目团队
3. 自媒体和内容创作者
4. 产品经理
5. 经常处理文档和网页资料的办公用户
```

不要一开始打泛人群。初版应该先从“高频使用 AI + 经常在电脑上工作”的人群开始。

---

# 19. 商业模式初版

## 免费版

```text
1. 桌面小人
2. 基础聊天
3. 少量屏幕总结
4. 少量任务历史
5. 手动输入任务
```

## Pro 版

```text
1. 屏幕上下文理解
2. 浏览器页面总结
3. 本地文件读取
4. 项目目录诊断
5. 长期记忆
6. Hermes 工作流
7. 多模型配置
8. 任务历史
```

## 早鸟买断版

```text
价格：99—199 元
权益：永久基础功能 + 1 年更新
```

## 月订阅

```text
价格：29—59 元/月
适合：重度办公、开发、创作用户
```

---

# 20. 最大风险和处理方式

## 风险 1：许可证风险

处理：

```text
1. Hermes 可作为核心依赖优先评估。
2. OpenHuman 只参考，不复制代码。
3. Everywhere 初期用于验证，商业版逐步替换为自研 Context Adapter 或取得授权。
```

OpenHuman 文档显示其使用 GPL3，且定位中包含 desktop mascot、Memory Tree、本地知识库等特性，因此它更适合做产品体验参考，而不是商业闭源产品的代码依赖。citeturn627872search14

---

## 风险 2：用户不信任 AI 操作电脑

处理：

```text
1. 所有高危操作显式确认
2. 工具调用透明展示
3. 权限可撤销
4. 任务可中断
5. 日志可查看
```

---

## 风险 3：小人变成“套皮聊天框”

处理：

```text
1. 强化屏幕理解能力
2. 强化任务工作流
3. 强化状态动画
4. 强化记忆延续
5. 不把聊天作为主卖点
```

---

## 风险 4：功能过多导致做不完

处理：

```text
初版只死磕 3 个场景：
1. 当前网页总结
2. 当前报错分析
3. 项目目录诊断
```

---

# 21. 你现在应该马上开始做的事项

按这个顺序执行：

```text
1. 建立项目仓库
2. 写 state-machine.md
3. 做桌面悬浮小人空壳
4. 做 5 个基础动画：Idle / Listening / Thinking / Working / Completed
5. 做本地服务和 WebSocket 事件流
6. 接 Hermes Adapter
7. 做 Task Router
8. 接当前窗口 / 选中文本 / 浏览器上下文
9. 做文件读取和项目目录扫描
10. 做权限确认弹窗
11. 做 Memory Center
12. 打包 Windows 安装包
```

最小闭环必须先跑通：

```text
用户输入任务
  ↓
小人进入 Thinking
  ↓
读取当前上下文
  ↓
Hermes 执行
  ↓
小人进入 Working
  ↓
需要权限则弹窗
  ↓
返回结果
  ↓
用户确认是否保存记忆
  ↓
小人进入 Completed
```

---

# 22. 初版一句话总结

你的初版方案应该定为：

> **基于 Windows 的 2D 桌面 AI 小人，以 Everywhere 验证桌面上下文能力，以 Hermes 作为长期任务大脑，参考 OpenHuman 的桌面人格化和可读记忆设计，先实现网页总结、报错分析、项目诊断三个核心场景，并用权限确认和手动记忆机制建立商业产品的安全底线。**