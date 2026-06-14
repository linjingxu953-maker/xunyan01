namespace DesktopMascot.Core.Enums;

/// <summary>
/// 任务类型
/// </summary>
public enum TaskType
{
    /// <summary>普通问答</summary>
    Chat,
    /// <summary>总结当前网页</summary>
    SummarizePage,
    /// <summary>分析当前报错</summary>
    AnalyzeError,
    /// <summary>分析项目目录</summary>
    InspectProject,
    /// <summary>生成文件</summary>
    WriteFile,
    /// <summary>执行命令</summary>
    RunCommand,
    /// <summary>记忆更新</summary>
    UpdateMemory,
    /// <summary>屏幕理解（区域圈选）</summary>
    ScreenUnderstand,
    /// <summary>题目解答</summary>
    SolveProblem,
    /// <summary>计算机操作（鼠标/键盘/窗口）</summary>
    ComputerUse
}
