using DesktopMascot.Core.Enums;

namespace DesktopMascot.UI.ViewModels;

public static class ToolLauncherCatalog
{
    public const string AllCategory = "全部";

    public static IReadOnlyList<ToolLauncherItem> CreateDefaultItems() =>
    [
        new("screen_understand", "屏幕圈选理解", "屏幕", "圈选屏幕区域，让视觉模型识别内容、提取文本并给出下一步。", TaskType.ScreenUnderstand, "screen_understand", "如果还没有圈选区域，请先触发屏幕圈选；如果已有截图引用，请结合识别结果继续分析。", "L2 屏幕", ToolLauncherLaunchMode.ScreenSelection, "截图", "OCR", "识别", "圈选"),
        new("screen_capture", "屏幕截图", "屏幕", "捕获当前屏幕或窗口，用于后续理解、记录和排查。", TaskType.SummarizePage, "screen_capture", "获取当前屏幕上下文并说明截图用途。", "L2 屏幕", "capture", "screenshot", "窗口"),
        new("browser_context", "浏览器上下文", "屏幕", "读取当前浏览器标题、页面信息或截图，适合网页总结和资料整理。", TaskType.SummarizePage, "browser_context", "读取当前浏览器内容并总结重点。", "L2 浏览器", "网页", "浏览器", "总结"),
        new("clipboard", "剪贴板读取", "屏幕", "读取剪贴板文本，用于改写、翻译、总结或继续处理。", TaskType.Chat, "clipboard", "读取剪贴板内容并按用户目标处理。", "L1 上下文", "复制", "粘贴", "clipboard"),

        new("read_file", "读取文件", "文件", "读取本地文件内容并进行总结、检查或转换。", TaskType.InspectProject, "read_file", "读取指定文件并说明要提取的信息。", "L3 文件读取", "文件", "read"),
        new("list_directory", "目录浏览", "文件", "查看目录结构，适合项目诊断和资料归档。", TaskType.InspectProject, "list_directory", "列出目标目录并总结关键文件。", "L3 文件读取", "目录", "文件夹", "project"),
        new("search_file", "搜索文件", "文件", "按文件名或内容搜索项目和资料目录。", TaskType.InspectProject, "search_file", "搜索目标关键词并返回命中的文件与上下文。", "L3 文件读取", "搜索", "grep", "查找"),
        new("write_file", "写入文件", "文件", "生成新文件或保存整理后的内容。", TaskType.WriteFile, "write_file", "生成完整文件内容，并在执行前等待文件写入确认。", "L4 文件写入", "保存", "生成文件", "写文件"),
        new("edit_file", "编辑文件", "文件", "替换、插入、删除或追加文件内容。", TaskType.WriteFile, "edit_file", "说明目标文件、编辑范围和期望修改。", "L4 文件写入", "修改", "替换", "append"),
        new("file_compare", "文件对比", "文件", "比较两个文件差异并输出摘要。", TaskType.InspectProject, "file_compare", "比较两个文件并解释差异。", "L3 文件读取", "diff", "compare"),
        new("file_organizer", "文件整理", "文件", "按类型、时间或规则整理目录文件。", TaskType.WriteFile, "file_organizer", "说明整理目录和规则，执行前确认文件移动。", "L4 文件写入", "整理", "归档"),
        new("compression", "压缩解压", "文件", "创建压缩包或解压资料包。", TaskType.WriteFile, "compression", "说明压缩或解压目标路径。", "L4 文件写入", "zip", "解压"),
        new("file_encryption", "文件加密", "文件", "加密或解密本地文件和文件夹。", TaskType.WriteFile, "file_encryption", "说明加密目标和输出要求，敏感操作前确认。", "L4 文件写入", "加密", "解密"),

        new("run_command", "执行命令", "执行", "运行本地命令，适合构建、测试、诊断。", TaskType.RunCommand, "run_command", "说明要运行的命令、目录和目的，执行前等待确认。", "L5 命令", "terminal", "PowerShell", "命令"),
        new("computer_use", "桌面自动操作", "执行", "规划并执行鼠标、键盘、窗口操作。", TaskType.ComputerUse, "computer_use", "描述桌面目标和允许的操作范围，敏感动作需要确认。", "L2-L5 桌面", ToolLauncherLaunchMode.ComputerUsePanel, "鼠标", "键盘", "自动操作"),
        new("browser_automation", "浏览器自动化", "执行", "打开网页、点击、输入、读取页面状态。", TaskType.ComputerUse, "browser_automation", "说明目标网站和操作步骤，敏感输入需确认。", "L2 浏览器", ToolLauncherLaunchMode.ComputerUsePanel, "网页操作", "自动化"),
        new("task_template", "任务模板", "执行", "按模板生成常见任务流程。", TaskType.Chat, "task_template", "选择或描述任务模板并生成执行步骤。", "L0 对话", "workflow", "模板"),
        new("concurrency_control", "并发任务控制", "执行", "查看或控制后台任务并发状态。", TaskType.Chat, "concurrency_control", "说明需要暂停、恢复或检查的任务。", "L0 对话", "并发", "队列"),

        new("code_analysis", "代码分析", "开发", "分析代码质量、复杂度、潜在问题和改进点。", TaskType.InspectProject, "code_analysis", "说明代码路径和关注点，例如质量、bug、性能或安全。", "L3 文件读取", "代码", "质量评分", "review"),
        new("security_scan", "安全扫描", "开发", "扫描代码、配置或文本中的安全风险。", TaskType.InspectProject, "security_scan", "说明扫描目标和风险范围。", "L3 文件读取", "漏洞", "安全", "secret"),
        new("database", "数据库操作", "开发", "执行查询、导入导出或检查数据库结构。", TaskType.RunCommand, "database", "说明数据库位置、查询目标和是否只读。", "L4 数据", "SQL", "sqlite"),
        new("network_request", "网络请求", "开发", "发送 HTTP 请求、检查 API 返回或调试接口。", TaskType.RunCommand, "network_request", "说明 URL、方法、参数和认证方式。", "L3 网络", "HTTP", "API"),
        new("performance", "性能分析", "开发", "分析性能瓶颈、耗时和资源占用。", TaskType.InspectProject, "performance_analysis", "说明性能问题、复现方式和目标范围。", "L3 文件读取", "性能", "profile"),

        new("ocr", "OCR 文字识别", "内容", "从图片或截图提取文字。", TaskType.ScreenUnderstand, "ocr", "提供图片或截图路径，并说明要提取哪些文字。", "L2 屏幕", ToolLauncherLaunchMode.ScreenSelection, "图片文字", "识别"),
        new("translate", "翻译", "内容", "翻译文本并保留语气、格式或术语。", TaskType.Chat, "translate", "提供待翻译内容和目标语言。", "L0 对话", "英文", "中文", "翻译"),
        new("pdf", "PDF 处理", "内容", "提取、总结、拆分或合并 PDF 内容。", TaskType.InspectProject, "pdf_tool", "说明 PDF 路径和处理目标。", "L3 文件读取", "PDF", "文档"),
        new("note_generator", "笔记生成", "内容", "从资料、网页或课堂内容生成结构化笔记。", TaskType.Chat, "note_generator", "提供资料来源和笔记格式要求。", "L0 对话", "笔记", "总结"),
        new("paper_writing", "论文写作", "内容", "生成论文提纲、段落、润色和引用整理。", TaskType.Chat, "paper_writing", "说明主题、字数、格式和引用要求。", "L0 对话", "论文", "写作"),
        new("course_assist", "课程作业", "内容", "处理大学生作业、网课资料和题目讲解。", TaskType.SolveProblem, "course_assist", "提供题目、课程材料或截图，并说明要解题还是整理。", "L0 对话", "作业", "题目", "网课"),
        new("exam_mode", "考试/刷题", "内容", "解析题目、生成答案和步骤说明。", TaskType.SolveProblem, "exam_mode", "提供题目内容并说明是否需要详细步骤。", "L0 对话", "刷题", "答案"),

        new("image_processing", "图片处理", "媒体", "裁剪、格式转换、压缩或基础图片处理。", TaskType.WriteFile, "image_processing", "说明图片路径和处理方式。", "L4 文件写入", "图片", "裁剪", "压缩"),
        new("video_processing", "视频处理", "媒体", "剪切、转码、提取音频或检查视频信息。", TaskType.WriteFile, "video_processing", "说明视频路径、处理片段和输出格式。", "L4 文件写入", "视频", "转码", "ffmpeg"),
        new("short_video", "短视频生成", "媒体", "生成短视频脚本、素材流程或合成任务。", TaskType.WriteFile, "short_video_maker", "说明主题、时长、风格和素材来源。", "L4 文件写入", "短视频", "剪视频"),
        new("text_to_speech", "文字转语音", "媒体", "把文本转成语音或试听角色语音。", TaskType.Chat, "text_to_speech", "提供要朗读的文本和声音要求。", "L0 对话", "TTS", "语音"),

        new("calendar", "日程管理", "个人", "创建、查看或整理日程计划。", TaskType.Chat, "calendar", "说明日程内容、时间和提醒要求。", "L0 对话", "日程", "计划"),
        new("email", "邮件草稿", "个人", "生成邮件、回复或整理邮件内容。", TaskType.Chat, "email", "提供收件场景和邮件目标。", "L0 对话", "邮件", "回复"),
        new("notification", "通知提醒", "个人", "创建本地通知、提醒或查看通知历史。", TaskType.Chat, "notification", "说明提醒内容和时间。", "L0 对话", "提醒", "toast"),
        new("cloud_sync", "云同步", "个人", "同步文件到云端或检查同步计划。", TaskType.WriteFile, "cloud_sync", "说明同步源、目标和冲突处理方式。", "L4 文件写入", "云盘", "同步"),
        new("character_switch", "角色切换", "角色", "切换当前角色或应用角色包。", TaskType.Chat, "character_switch", "说明要切换的角色或导入的角色包。", "L0 对话", "角色", "人物"),
        new("character_market", "角色市场", "角色", "浏览、导入或管理角色市场条目。", TaskType.Chat, "character_market", "说明想找的角色风格或管理动作。", "L0 对话", "角色包", "市场")
    ];

    public static IReadOnlyList<string> CreateCategories(IEnumerable<ToolLauncherItem> items) =>
        [AllCategory, .. items.Select(item => item.Category).Distinct().OrderBy(category => category)];

    public static IEnumerable<ToolLauncherItem> Filter(
        IEnumerable<ToolLauncherItem> items,
        string? query,
        string? category)
    {
        var selectedCategory = string.IsNullOrWhiteSpace(category) ? AllCategory : category.Trim();

        return items
            .Where(item => selectedCategory == AllCategory || item.Category == selectedCategory)
            .Where(item => item.Matches(query ?? string.Empty))
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Title);
    }
}
