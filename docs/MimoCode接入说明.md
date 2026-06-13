# Mimo Code 接入说明

## 接入边界

- 桌面小人只保存本机 Mimo Code 接入配置：启用开关、可执行路径、工作目录、模型配置来源。
- 模型调用必须使用用户自己的 API 配置，不在应用内置、上传或代管任何 Provider API Key。
- 用户可以选择使用桌面小人设置中心里的 Provider/API Key，或使用本机已有的 Mimo Code 配置。
- 文件写入、命令执行、记忆保存等高风险操作仍走桌面小人的权限确认和记忆确认弹窗。

## MIT 协议要求

Mimo Code 使用 MIT 开源协议。后续如果把 Mimo Code 的源码、二进制或其派生代码随本项目分发，需要保留原项目的版权声明和许可证文本。

项目根目录的 `THIRD_PARTY_LICENSES.md` 已记录 MiMo Code 的 MIT 许可证文本，打包/分发时需要一并保留。

当前阶段仅做本机接入配置页，不复制或分发 Mimo Code 源码。

## 后续接入点

- 读取 `AppSettings.MimoCodeEnabled` 判断是否启用。
- 读取 `AppSettings.MimoCodeExecutablePath` 定位本机 Mimo Code。
- 读取 `AppSettings.MimoCodeWorkspaceDirectory` 作为默认工作目录。
- 读取 `AppSettings.MimoCodeModelConfigMode` 判断模型配置来源：
  - `AppProvider`：使用桌面小人设置中心保存的 Provider/API Key。
  - `MimoLocalConfig`：使用用户本机已有的 Mimo Code 配置。
