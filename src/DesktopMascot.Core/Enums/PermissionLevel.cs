namespace DesktopMascot.Core.Enums;

/// <summary>
/// 权限等级
/// </summary>
public enum PermissionLevel
{
    /// <summary>普通聊天 - 不需要确认</summary>
    L0_Chat = 0,
    /// <summary>读取当前窗口 - 首次需要</summary>
    L1_WindowTitle = 1,
    /// <summary>读取屏幕/浏览器 - 首次需要</summary>
    L2_ScreenBrowser = 2,
    /// <summary>读取授权文件 - 需要目录授权</summary>
    L3_FileRead = 3,
    /// <summary>写入文件 - 每次确认</summary>
    L4_FileWrite = 4,
    /// <summary>执行命令 - 每次确认</summary>
    L5_CommandExec = 5,
    /// <summary>删除/支付/发送/发布 - 初版禁止</summary>
    L6_Forbidden = 6
}
