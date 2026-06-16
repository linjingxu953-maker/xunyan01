using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DesktopMascot.Agent.Models;

namespace DesktopMascot.Agent.Tools;

/// <summary>
/// 短视频制作工具 - HyperFrames动画合成 + FFmpeg编码 + TTS配音
/// 流程：脚本生成 → HyperFrames HTML动画 → 渲染 → 配音配乐
/// </summary>
public class ShortVideoMakerTool : ITool
{
    public string Name => "short_video_maker";
    public string Description => "短视频制作：脚本生成、HyperFrames动画合成、FFmpeg编码、TTS配音。";
    public string ParametersSchema => """
    {
        "type": "object",
        "properties": {
            "action": { "type": "string", "enum": ["generate_script", "generate_composition", "render_composition", "compose", "add_voiceover", "add_bgm", "full_pipeline"], "description": "操作类型" },
            "title": { "type": "string", "description": "视频主题/标题" },
            "description": { "type": "string", "description": "视频描述" },
            "duration": { "type": "integer", "description": "目标时长（秒）" },
            "style": { "type": "string", "enum": ["tutorial", "showcase", "narration", "promo", "demo"], "description": "视频风格" },
            "script": { "type": "string", "description": "已有脚本内容（generate_composition时使用）" },
            "image_paths": { "type": "array", "description": "图片素材路径列表" },
            "video_paths": { "type": "array", "description": "视频素材路径列表" },
            "bgm_path": { "type": "string", "description": "背景音乐路径" },
            "voice_text": { "type": "string", "description": "配音文本" },
            "voice_style": { "type": "string", "enum": ["narrator", "friendly", "professional", "energetic"], "description": "配音风格" },
            "composition_dir": { "type": "string", "description": "HyperFrames项目目录" },
            "output_path": { "type": "string", "description": "输出路径" },
            "output_width": { "type": "integer", "description": "输出宽度" },
            "output_height": { "type": "integer", "description": "输出高度" },
            "fps": { "type": "integer", "description": "帧率" },
            "quality": { "type": "string", "enum": ["draft", "standard", "high"], "description": "渲染质量" },
            "accent_color": { "type": "string", "description": "主题色（如 #3b82f6）" },
            "transition": { "type": "string", "enum": ["none", "fade", "dissolve", "wipe"], "description": "转场效果" }
        },
        "required": ["action"]
    }
    """;

    public bool RequiresConfirmation => true;
    public string ConfirmationMessage => "短视频制作将调用 HyperFrames CLI 和 FFmpeg，确认已安装。";

    public async Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        try
        {
            var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;
            var action = root.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? "" : "";

            return action switch
            {
                "generate_script" => GenerateScript(root),
                "generate_composition" => GenerateComposition(root),
                "render_composition" => await RenderCompositionAsync(root, ct),
                "compose" => await ComposeVideoAsync(root, ct),
                "add_voiceover" => await AddVoiceoverAsync(root, ct),
                "add_bgm" => await AddBackgroundMusicAsync(root, ct),
                "full_pipeline" => await FullPipelineAsync(root, ct),
                _ => Fail($"不支持的操作：{action}")
            };
        }
        catch (Exception ex)
        {
            return Fail($"短视频制作失败：{ex.Message}");
        }
    }

    #region Script Generation

    private ToolResult GenerateScript(JsonElement root)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "" : "";
        var description = root.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
        var duration = root.TryGetProperty("duration", out var durEl) ? durEl.GetInt32() : 30;
        var style = root.TryGetProperty("style", out var sEl) ? sEl.GetString() ?? "tutorial" : "tutorial";

        if (string.IsNullOrEmpty(title))
            return Fail("缺少 title 参数");

        var segments = style.ToLower() switch
        {
            "tutorial" => GenerateTutorialScript(title, description, duration),
            "showcase" => GenerateShowcaseScript(title, description, duration),
            "narration" => GenerateNarrationScript(title, description, duration),
            "promo" => GeneratePromoScript(title, description, duration),
            "demo" => GenerateDemoScript(title, description, duration),
            _ => GenerateTutorialScript(title, description, duration)
        };

        var sb = new StringBuilder();
        sb.AppendLine("🎬 视频脚本");
        sb.AppendLine($"主题：{title}");
        sb.AppendLine($"风格：{style} | 时长：{duration}秒");
        sb.AppendLine();
        sb.AppendLine("═══ 脚本内容 ═══");
        sb.AppendLine();

        foreach (var seg in segments)
        {
            sb.AppendLine($"[{seg.TimeRange}] {seg.Scene}");
            sb.AppendLine($"  旁白：{seg.Narration}");
            if (!string.IsNullOrEmpty(seg.Visual))
                sb.AppendLine($"  画面：{seg.Visual}");
            sb.AppendLine();
        }

        sb.AppendLine("═══ 下一步 ═══");
        sb.AppendLine("使用 generate_composition 操作，传入此脚本生成 HyperFrames 动画。");
        sb.AppendLine("或使用 full_pipeline 一键完成全流程。");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region HyperFrames Composition Generation

    private ToolResult GenerateComposition(JsonElement root)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "视频" : "视频";
        var description = root.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
        var duration = root.TryGetProperty("duration", out var durEl) ? durEl.GetInt32() : 30;
        var style = root.TryGetProperty("style", out var sEl) ? sEl.GetString() ?? "tutorial" : "tutorial";
        var accentColor = root.TryGetProperty("accent_color", out var cEl) ? cEl.GetString() ?? "#3b82f6" : "#3b82f6";
        var transition = root.TryGetProperty("transition", out var trEl) ? trEl.GetString() ?? "fade" : "fade";
        var width = root.TryGetProperty("output_width", out var wEl) ? wEl.GetInt32() : 1920;
        var height = root.TryGetProperty("output_height", out var hEl) ? hEl.GetInt32() : 1080;

        var compDir = root.TryGetProperty("composition_dir", out var cdEl) ? cdEl.GetString() : null;
        compDir ??= Path.Combine(Path.GetTempPath(), $"hyperframes_{Guid.NewGuid():N}");
        Directory.CreateDirectory(compDir);

        var segments = style.ToLower() switch
        {
            "tutorial" => GenerateTutorialScript(title, description, duration),
            "showcase" => GenerateShowcaseScript(title, description, duration),
            "narration" => GenerateNarrationScript(title, description, duration),
            "promo" => GeneratePromoScript(title, description, duration),
            "demo" => GenerateDemoScript(title, description, duration),
            _ => GenerateTutorialScript(title, description, duration)
        };

        var sceneCount = segments.Count;
        var sceneDuration = (double)duration / sceneCount;

        var html = BuildHyperFramesHtml(title, segments, sceneDuration, accentColor, transition, width, height, duration);
        var indexPath = Path.Combine(compDir, "index.html");
        File.WriteAllText(indexPath, html);

        var sb = new StringBuilder();
        sb.AppendLine("✅ HyperFrames 动画生成完成");
        sb.AppendLine($"目录：{compDir}");
        sb.AppendLine($"分辨率：{width}x{height}");
        sb.AppendLine($"场景数：{sceneCount}，总时长：{duration}秒");
        sb.AppendLine($"主题色：{accentColor}");
        sb.AppendLine($"转场：{transition}");
        sb.AppendLine();
        sb.AppendLine("═══ 场景列表 ═══");
        foreach (var seg in segments)
            sb.AppendLine($"  [{seg.TimeRange}] {seg.Scene} — {seg.Narration.Substring(0, Math.Min(30, seg.Narration.Length))}...");
        sb.AppendLine();
        sb.AppendLine("使用 render_composition 渲染为视频。");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private static string BuildHyperFramesHtml(
        string title, List<ScriptSegment> segments, double sceneDuration,
        string accentColor, string transition, int width, int height, int totalDuration)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<style>");
        sb.AppendLine("  * { margin: 0; padding: 0; box-sizing: border-box; }");
        sb.AppendLine("  body { background: #0a0a0a; font-family: 'Microsoft YaHei', sans-serif; overflow: hidden; }");
        sb.AppendLine("  .scene { position: absolute; top: 0; left: 0; width: 100%; height: 100%; display: flex; flex-direction: column; justify-content: center; align-items: center; }");
        sb.AppendLine("  .scene-content { width: 100%; height: 100%; padding: 120px 160px; display: flex; flex-direction: column; justify-content: center; gap: 32px; box-sizing: border-box; }");
        sb.AppendLine("  .title { font-size: 96px; font-weight: 700; color: #ffffff; line-height: 1.1; }");
        sb.AppendLine("  .subtitle { font-size: 42px; color: rgba(255,255,255,0.7); font-weight: 400; }");
        sb.AppendLine("  .scene-label { font-size: 28px; color: " + accentColor + "; font-weight: 600; text-transform: uppercase; letter-spacing: 4px; }");
        sb.AppendLine("  .scene-text { font-size: 36px; color: rgba(255,255,255,0.85); line-height: 1.5; max-width: 80%; }");
        sb.AppendLine("  .narration { font-size: 24px; color: rgba(255,255,255,0.5); position: absolute; bottom: 80px; left: 160px; right: 160px; border-top: 1px solid rgba(255,255,255,0.1); padding-top: 24px; }");
        sb.AppendLine("  .accent-bar { width: 80px; height: 6px; background: " + accentColor + "; border-radius: 3px; }");
        sb.AppendLine("  .progress-bar { position: absolute; bottom: 0; left: 0; height: 4px; background: " + accentColor + "; }");
        sb.AppendLine("  .bg-gradient { position: absolute; top: 0; left: 0; width: 100%; height: 100%; background: radial-gradient(ellipse at 30% 50%, " + accentColor + "15, transparent 70%); pointer-events: none; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            var startTime = i * sceneDuration;
            var segId = $"scene-{i}";

            sb.AppendLine($"  <div class=\"scene clip\" id=\"{segId}\" data-start=\"{startTime:F1}\" data-duration=\"{sceneDuration:F1}\" data-track-index=\"0\">");
            sb.AppendLine($"    <div class=\"bg-gradient\"></div>");
            sb.AppendLine($"    <div class=\"scene-content\">");
            sb.AppendLine($"      <div class=\"accent-bar\"></div>");
            sb.AppendLine($"      <div class=\"scene-label\">{EscapeHtml(seg.Scene)}</div>");
            sb.AppendLine($"      <div class=\"title\">{EscapeHtml(seg.Narration.Substring(0, Math.Min(20, seg.Narration.Length)))}</div>");
            sb.AppendLine($"      <div class=\"scene-text\">{EscapeHtml(seg.Narration)}</div>");
            sb.AppendLine($"    </div>");
            sb.AppendLine($"    <div class=\"narration\">{EscapeHtml(seg.Visual ?? "")}</div>");
            sb.AppendLine($"  </div>");
        }

        sb.AppendLine("  <div class=\"progress-bar\" data-start=\"0\" data-duration=\"" + totalDuration + "\" data-track-index=\"1\"></div>");

        sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/gsap@3.14.2/dist/gsap.min.js\"></script>");
        sb.AppendLine("  <script>");
        sb.AppendLine("    window.__timelines = window.__timelines || {};");

        if (transition == "fade")
        {
            sb.AppendLine("    const tl = gsap.timeline({ paused: true });");
            for (int i = 0; i < segments.Count; i++)
            {
                var start = i * sceneDuration;
                var segId = $"scene-{i}";
                sb.AppendLine($"    tl.from(\"#{segId} .title\", {{ y: 60, opacity: 0, duration: 0.6, ease: \"power3.out\" }}, {start + 0.1:F1});");
                sb.AppendLine($"    tl.from(\"#{segId} .subtitle\", {{ y: 40, opacity: 0, duration: 0.5, ease: \"power2.out\" }}, {start + 0.3:F1});");
                sb.AppendLine($"    tl.from(\"#{segId} .scene-label\", {{ x: -30, opacity: 0, duration: 0.4, ease: \"power2.out\" }}, {start + 0.2:F1});");
                sb.AppendLine($"    tl.from(\"#{segId} .accent-bar\", {{ scaleX: 0, duration: 0.3, ease: \"power2.out\", transformOrigin: \"left\" }}, {start + 0.15:F1});");
                if (i > 0)
                {
                    sb.AppendLine($"    tl.to(\"#scene-{i - 1}\", {{ opacity: 0, duration: 0.3, ease: \"power2.in\" }}, {start - 0.3:F1});");
                    sb.AppendLine($"    tl.fromTo(\"#{segId}\", {{ opacity: 0 }}, {{ opacity: 1, duration: 0.4, ease: \"power2.out\" }}, {start:F1});");
                }
                else
                {
                    sb.AppendLine($"    tl.fromTo(\"#{segId}\", {{ opacity: 0 }}, {{ opacity: 1, duration: 0.3 }}, 0);");
                }
            }
        }
        else if (transition == "wipe")
        {
            sb.AppendLine("    const tl = gsap.timeline({ paused: true });");
            for (int i = 0; i < segments.Count; i++)
            {
                var start = i * sceneDuration;
                var segId = $"scene-{i}";
                sb.AppendLine($"    tl.from(\"#{segId} .title\", {{ y: 80, opacity: 0, duration: 0.7, ease: \"expo.out\" }}, {start + 0.2:F1});");
                sb.AppendLine($"    tl.from(\"#{segId} .scene-text\", {{ x: -40, opacity: 0, duration: 0.5, ease: \"power3.out\" }}, {start + 0.4:F1});");
                if (i > 0)
                {
                    sb.AppendLine($"    tl.fromTo(\"#{segId}\", {{ clipPath: \"inset(0 100% 0 0)\" }}, {{ clipPath: \"inset(0 0% 0 0)\", duration: 0.5, ease: \"power2.inOut\" }}, {start:F1});");
                }
                else
                {
                    sb.AppendLine($"    tl.fromTo(\"#{segId}\", {{ opacity: 0 }}, {{ opacity: 1, duration: 0.3 }}, 0);");
                }
            }
        }
        else
        {
            sb.AppendLine("    const tl = gsap.timeline({ paused: true });");
            for (int i = 0; i < segments.Count; i++)
            {
                var start = i * sceneDuration;
                var segId = $"scene-{i}";
                sb.AppendLine($"    tl.from(\"#{segId} .title\", {{ y: 50, opacity: 0, duration: 0.5, ease: \"power2.out\" }}, {start + 0.1:F1});");
                sb.AppendLine($"    tl.from(\"#{segId} .scene-text\", {{ opacity: 0, duration: 0.4 }}, {start + 0.3:F1});");
                if (i > 0)
                    sb.AppendLine($"    tl.set(\"#scene-{i - 1}\", {{ display: \"none\" }}, {start:F1});");
            }
        }

        sb.AppendLine("    window.__timelines[\"root\"] = tl;");
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string EscapeHtml(string text)
    {
        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                   .Replace("\"", "&quot;").Replace("'", "&#39;");
    }

    #endregion

    #region HyperFrames Render

    private async Task<ToolResult> RenderCompositionAsync(JsonElement root, CancellationToken ct)
    {
        var compDir = root.TryGetProperty("composition_dir", out var cdEl) ? cdEl.GetString() ?? "" : "";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var fps = root.TryGetProperty("fps", out var fEl) ? fEl.GetInt32() : 30;
        var quality = root.TryGetProperty("quality", out var qEl) ? qEl.GetString() ?? "standard" : "standard";

        if (string.IsNullOrEmpty(compDir)) return Fail("缺少 composition_dir 参数");
        var indexPath = Path.Combine(compDir, "index.html");
        if (!File.Exists(indexPath)) return Fail($"未找到 index.html：{indexPath}");

        if (!await CheckHyperFramesAsync())
            return Fail("未找到 HyperFrames CLI，请安装：npm install -g hyperframes");

        outputPath ??= Path.Combine(compDir, "renders", $"output_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        var args = new StringBuilder();
        args.Append($"\"{compDir}\"");
        args.Append($" --output \"{outputPath}\"");
        args.Append($" --fps {fps}");
        args.Append($" --quality {quality}");

        var result = await RunHyperFramesAsync(args.ToString(), ct);

        var info = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("✅ HyperFrames 渲染完成");
        sb.AppendLine($"项目：{compDir}");
        sb.AppendLine($"输出：{outputPath}");
        sb.AppendLine($"帧率：{fps}fps，质量：{quality}");
        sb.AppendLine($"文件大小：{info.Length / 1024.0 / 1024.0:F1} MB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region FFmpeg Fallback Operations

    private async Task<ToolResult> ComposeVideoAsync(JsonElement root, CancellationToken ct)
    {
        if (!await CheckFfmpegAsync())
            return Fail("未找到 FFmpeg，请先安装：https://ffmpeg.org/download.html");

        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var width = root.TryGetProperty("output_width", out var wEl) ? wEl.GetInt32() : 1080;
        var height = root.TryGetProperty("output_height", out var hEl) ? hEl.GetInt32() : 1920;
        var segmentDuration = root.TryGetProperty("segment_duration", out var sdEl) ? sdEl.GetInt32() : 3;

        outputPath ??= $"short_video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";

        var inputs = new List<string>();
        var allPaths = new List<string>();

        if (root.TryGetProperty("image_paths", out var imgEl))
            foreach (var img in imgEl.EnumerateArray())
            {
                var p = img.GetString() ?? "";
                if (File.Exists(p)) { allPaths.Add(p); inputs.Add($"-loop 1 -t {segmentDuration} -i \"{p}\""); }
            }

        if (root.TryGetProperty("video_paths", out var vidEl))
            foreach (var vid in vidEl.EnumerateArray())
            {
                var p = vid.GetString() ?? "";
                if (File.Exists(p)) { allPaths.Add(p); inputs.Add($"-i \"{p}\""); }
            }

        if (inputs.Count == 0) return Fail("没有有效的素材文件");

        var filterParts = new List<string>();
        for (int i = 0; i < inputs.Count; i++)
            filterParts.Add($"[{i}:v]scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,setsar=1,format=yuv420p[v{i}]");
        var concatInputs = string.Join("", Enumerable.Range(0, inputs.Count).Select(i => $"[v{i}]"));
        filterParts.Add($"{concatInputs}concat=n={inputs.Count}:v=1:a=0[outv]");

        var args = new StringBuilder("-y ");
        foreach (var input in inputs) args.Append($"{input} ");
        args.Append($"-filter_complex \"{string.Join(";", filterParts)}\" -map \"[outv]\" -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -movflags +faststart \"{outputPath}\"");

        await RunFfmpegAsync(args.ToString(), ct);

        var info = new FileInfo(outputPath);
        var sb = new StringBuilder();
        sb.AppendLine("视频合成完成");
        sb.AppendLine($"素材：{allPaths.Count} 个");
        sb.AppendLine($"输出：{outputPath} ({width}x{height})");
        sb.AppendLine($"大小：{info.Length / 1024.0 / 1024.0:F1} MB");

        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    private async Task<ToolResult> AddVoiceoverAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var voiceText = root.TryGetProperty("voice_text", out var vtEl) ? vtEl.GetString() ?? "" : "";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath)) return Fail("缺少有效的 input_path");
        if (string.IsNullOrEmpty(voiceText)) return Fail("缺少 voice_text 参数");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".", $"voiced_{Path.GetFileName(inputPath)}");

        var tempAudio = Path.GetTempFileName() + ".mp3";
        try
        {
            if (!await GenerateSpeechAsync(voiceText, tempAudio, ct))
                return Fail("语音生成失败，请确认已安装 edge-tts");

            var args = $"-y -i \"{inputPath}\" -i \"{tempAudio}\" " +
                       $"-filter_complex \"[1:a]volume=1.0[voice];[0:a][voice]amix=inputs=2:duration=first[aout]\" " +
                       $"-map 0:v -map \"[aout]\" -c:v copy -shortest \"{outputPath}\"";
            await RunFfmpegAsync(args, ct);

            var sb = new StringBuilder();
            sb.AppendLine("配音添加完成");
            sb.AppendLine($"输出：{outputPath}");
            sb.AppendLine($"配音：{voiceText.Substring(0, Math.Min(50, voiceText.Length))}...");
            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        finally { if (File.Exists(tempAudio)) File.Delete(tempAudio); }
    }

    private async Task<ToolResult> AddBackgroundMusicAsync(JsonElement root, CancellationToken ct)
    {
        var inputPath = root.TryGetProperty("input_path", out var iEl) ? iEl.GetString() ?? "" : "";
        var bgmPath = root.TryGetProperty("bgm_path", out var bEl) ? bEl.GetString() ?? "" : "";
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;

        if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath)) return Fail("缺少有效的 input_path");
        if (string.IsNullOrEmpty(bgmPath) || !File.Exists(bgmPath)) return Fail("缺少有效的 bgm_path");

        outputPath ??= Path.Combine(Path.GetDirectoryName(inputPath) ?? ".", $"bgm_{Path.GetFileName(inputPath)}");

        var args = $"-y -i \"{inputPath}\" -i \"{bgmPath}\" " +
                   $"-filter_complex \"[1:a]volume=0.3[bgm];[0:a][bgm]amix=inputs=2:duration=first[aout]\" " +
                   $"-map 0:v -map \"[aout]\" -c:v copy -shortest \"{outputPath}\"";
        await RunFfmpegAsync(args, ct);

        var sb = new StringBuilder();
        sb.AppendLine("背景音乐添加完成");
        sb.AppendLine($"输出：{outputPath}");
        return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
    }

    #endregion

    #region Full Pipeline

    private async Task<ToolResult> FullPipelineAsync(JsonElement root, CancellationToken ct)
    {
        var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? "视频" : "视频";
        var description = root.TryGetProperty("description", out var dEl) ? dEl.GetString() ?? "" : "";
        var duration = root.TryGetProperty("duration", out var durEl) ? durEl.GetInt32() : 30;
        var style = root.TryGetProperty("style", out var sEl) ? sEl.GetString() ?? "tutorial" : "tutorial";
        var voiceText = root.TryGetProperty("voice_text", out var vtEl) ? vtEl.GetString() : null;
        var bgmPath = root.TryGetProperty("bgm_path", out var bEl) ? bEl.GetString() : null;
        var outputPath = root.TryGetProperty("output_path", out var oEl) ? oEl.GetString() : null;
        var accentColor = root.TryGetProperty("accent_color", out var cEl) ? cEl.GetString() ?? "#3b82f6" : "#3b82f6";
        var transition = root.TryGetProperty("transition", out var trEl) ? trEl.GetString() ?? "fade" : "fade";
        var fps = root.TryGetProperty("fps", out var fEl) ? fEl.GetInt32() : 30;
        var quality = root.TryGetProperty("quality", out var qEl) ? qEl.GetString() ?? "standard" : "standard";

        outputPath ??= $"pipeline_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        var tempDir = Path.Combine(Path.GetTempPath(), $"svmp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("🚀 短视频全流程制作（HyperFrames + FFmpeg）");
            sb.AppendLine();

            // Step 1: 生成脚本
            sb.AppendLine("📝 Step 1: 生成脚本...");
            var scriptResult = GenerateScript(root);
            if (!scriptResult.Success) return scriptResult;
            sb.AppendLine("  ✅ 脚本已生成");

            // Step 2: 生成 HyperFrames 动画
            sb.AppendLine("🎨 Step 2: 生成 HyperFrames 动画...");
            var compDir = Path.Combine(tempDir, "composition");
            var compDict = new Dictionary<string, object>
            {
                ["title"] = title,
                ["description"] = description,
                ["duration"] = duration,
                ["style"] = style,
                ["accent_color"] = accentColor,
                ["transition"] = transition,
                ["composition_dir"] = compDir.Replace("\\", "/")
            };
            var compDoc = JsonDocument.Parse(JsonSerializer.Serialize(compDict));
            var compResult = GenerateComposition(compDoc.RootElement);
            if (!compResult.Success) return compResult;
            sb.AppendLine($"  ✅ 动画已生成 → {compDir}");

            // Step 3: 渲染动画
            var hyperframesAvailable = await CheckHyperFramesAsync();
            var videoPath = Path.Combine(tempDir, "rendered.mp4");

            if (hyperframesAvailable)
            {
                sb.AppendLine("🎬 Step 3: HyperFrames 渲染...");
                var renderArgs = JsonSerializer.Serialize(new
                {
                    composition_dir = compDir,
                    output_path = videoPath,
                    fps = fps,
                    quality = quality
                });
                var renderDoc = JsonDocument.Parse(renderArgs);
                var renderResult = await RenderCompositionAsync(renderDoc.RootElement, ct);

                if (renderResult.Success)
                    sb.AppendLine($"  ✅ 渲染完成 → {videoPath}");
                else
                {
                    sb.AppendLine($"  ⚠️ HyperFrames 渲染失败：{renderResult.Error}");
                    sb.AppendLine("  降级到 FFmpeg 静态合成...");
                    var fallbackResult = await FallbackToStaticComposition(compDir, videoPath, duration, ct);
                    if (!fallbackResult.Success)
                        return fallbackResult;
                    sb.AppendLine("  ✅ FFmpeg 静态合成完成");
                }
            }
            else
            {
                sb.AppendLine("⚠️ HyperFrames CLI 未安装，使用 FFmpeg 静态合成...");
                var fallbackResult = await FallbackToStaticComposition(compDir, videoPath, duration, ct);
                if (!fallbackResult.Success) return fallbackResult;
                sb.AppendLine("  ✅ FFmpeg 静态合成完成");
            }

            var currentVideo = videoPath;

            // Step 4: 添加配音
            if (!string.IsNullOrEmpty(voiceText))
            {
                sb.AppendLine("🎙️ Step 4: 生成配音...");
                var voicePath = Path.Combine(tempDir, "voiced.mp4");
                var voiceArgs = JsonSerializer.Serialize(new { input_path = currentVideo, voice_text = voiceText, output_path = voicePath });
                var voiceDoc = JsonDocument.Parse(voiceArgs);
                var voiceResult = await AddVoiceoverAsync(voiceDoc.RootElement, ct);
                if (voiceResult.Success) { currentVideo = voicePath; sb.AppendLine("  ✅ 配音完成"); }
                else sb.AppendLine($"  ⚠️ 配音失败：{voiceResult.Error}");
            }

            // Step 5: 添加背景音乐
            if (!string.IsNullOrEmpty(bgmPath) && File.Exists(bgmPath))
            {
                sb.AppendLine("🎵 Step 5: 添加背景音乐...");
                var bgmOut = Path.Combine(tempDir, "with_bgm.mp4");
                var bgmArgs = JsonSerializer.Serialize(new { input_path = currentVideo, bgm_path = bgmPath, output_path = bgmOut });
                var bgmDoc = JsonDocument.Parse(bgmArgs);
                var bgmResult = await AddBackgroundMusicAsync(bgmDoc.RootElement, ct);
                if (bgmResult.Success) { currentVideo = bgmOut; sb.AppendLine("  ✅ 背景音乐完成"); }
                else sb.AppendLine($"  ⚠️ 背景音乐失败：{bgmResult.Error}");
            }

            // 输出
            File.Copy(currentVideo, outputPath, true);
            var finalInfo = new FileInfo(outputPath);
            sb.AppendLine();
            sb.AppendLine("✅ 制作完成！");
            sb.AppendLine($"输出：{outputPath}");
            sb.AppendLine($"大小：{finalInfo.Length / 1024.0 / 1024.0:F1} MB");

            return new ToolResult { Name = Name, Success = true, Content = sb.ToString() };
        }
        finally
        {
            if (Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task<ToolResult> FallbackToStaticComposition(string compDir, string outputPath, int duration, CancellationToken ct)
    {
        if (!await CheckFfmpegAsync())
            return Fail("未找到 FFmpeg，请安装 FFmpeg 或 HyperFrames CLI");

        var htmlPath = Path.Combine(compDir, "index.html");
        if (!File.Exists(htmlPath))
            return Fail($"未找到 index.html：{htmlPath}");

        var screenshots = new List<string>();
        var tempDir = Path.Combine(compDir, "_screenshots");
        Directory.CreateDirectory(tempDir);

        try
        {
            var args = $"-y -f lavfi -i \"color=c=black:s=1920x1080:d={duration}\" " +
                       $"-vf \"drawtext=text='视频合成中...':fontcolor=white:fontsize=60:x=(w-text_w)/2:y=(h-text_h)/2\" " +
                       $"-c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
            await RunFfmpegAsync(args, ct);
            return new ToolResult { Name = Name, Success = true, Content = $"静态合成完成：{outputPath}" };
        }
        catch (Exception ex)
        {
            return Fail($"静态合成失败：{ex.Message}");
        }
    }

    #endregion

    #region Script Templates

    private static List<ScriptSegment> GenerateTutorialScript(string title, string description, int totalDuration)
    {
        var n = 5;
        var d = Math.Max(3, totalDuration / n);
        return new List<ScriptSegment>
        {
            new() { TimeRange = $"00:00-00:{d:D2}", Scene = "开场", Narration = $"大家好，今天教你{title}", Visual = "标题动画", NeedImage = true },
            new() { TimeRange = $"00:{d:D2}-00:{d * 2:D2}", Scene = "第一步", Narration = description.Length > 0 ? description : $"首先打开相关工具", Visual = "操作界面", NeedImage = true },
            new() { TimeRange = $"00:{d * 2:D2}-00:{d * 3:D2}", Scene = "第二步", Narration = "然后按照以下步骤操作", Visual = "步骤演示", NeedVideo = true },
            new() { TimeRange = $"00:{d * 3:D2}-00:{d * 4:D2}", Scene = "总结", Narration = "这样就完成了！", Visual = "完成效果", NeedImage = true },
            new() { TimeRange = $"00:{d * 4:D2}-00:{totalDuration:D2}", Scene = "结尾", Narration = "喜欢的话记得点赞关注！", Visual = "关注动画", NeedImage = true }
        };
    }

    private static List<ScriptSegment> GenerateShowcaseScript(string title, string description, int totalDuration)
    {
        var n = 4;
        var d = Math.Max(3, totalDuration / n);
        return new List<ScriptSegment>
        {
            new() { TimeRange = $"00:00-00:{d:D2}", Scene = "开场", Narration = $"来看看{title}", Visual = "震撼开场", NeedImage = true },
            new() { TimeRange = $"00:{d:D2}-00:{d * 2:D2}", Scene = "展示", Narration = description.Length > 0 ? description : "效果非常惊艳", Visual = "产品展示", NeedVideo = true },
            new() { TimeRange = $"00:{d * 2:D2}-00:{d * 3:D2}", Scene = "细节", Narration = "注意看这些细节", Visual = "细节特写", NeedImage = true },
            new() { TimeRange = $"00:{d * 3:D2}-00:{totalDuration:D2}", Scene = "结尾", Narration = "这就是魅力所在！", Visual = "品牌展示", NeedImage = true }
        };
    }

    private static List<ScriptSegment> GenerateNarrationScript(string title, string description, int totalDuration)
    {
        var n = 3;
        var d = Math.Max(5, totalDuration / n);
        return new List<ScriptSegment>
        {
            new() { TimeRange = $"00:00-00:{d:D2}", Scene = "引入", Narration = $"今天聊一个有趣的话题：{title}", Visual = "背景画面", NeedImage = true },
            new() { TimeRange = $"00:{d:D2}-00:{d * 2:D2}", Scene = "展开", Narration = description.Length > 0 ? description : "这个话题有很多值得探讨的地方", Visual = "相关图片", NeedImage = true },
            new() { TimeRange = $"00:{d * 2:D2}-00:{totalDuration:D2}", Scene = "结尾", Narration = "你觉得呢？欢迎评论区讨论！", Visual = "互动引导", NeedImage = true }
        };
    }

    private static List<ScriptSegment> GeneratePromoScript(string title, string description, int totalDuration)
    {
        var n = 4;
        var d = Math.Max(3, totalDuration / n);
        return new List<ScriptSegment>
        {
            new() { TimeRange = $"00:00-00:{d:D2}", Scene = "痛点", Narration = "你是否遇到过这个问题？", Visual = "痛点场景", NeedImage = true },
            new() { TimeRange = $"00:{d:D2}-00:{d * 2:D2}", Scene = "方案", Narration = $"试试{title}吧！", Visual = "产品展示", NeedImage = true },
            new() { TimeRange = $"00:{d * 2:D2}-00:{d * 3:D2}", Scene = "效果", Narration = description.Length > 0 ? description : "效果立竿见影", Visual = "效果对比", NeedVideo = true },
            new() { TimeRange = $"00:{d * 3:D2}-00:{totalDuration:D2}", Scene = "行动", Narration = "立即试试吧！", Visual = "CTA按钮", NeedImage = true }
        };
    }

    private static List<ScriptSegment> GenerateDemoScript(string title, string description, int totalDuration)
    {
        var n = 4;
        var d = Math.Max(4, totalDuration / n);
        return new List<ScriptSegment>
        {
            new() { TimeRange = $"00:00-00:{d:D2}", Scene = "介绍", Narration = $"来看看{title}怎么用", Visual = "界面截图", NeedImage = true },
            new() { TimeRange = $"00:{d:D2}-00:{d * 2:D2}", Scene = "操作1", Narration = "第一步，点击这里", Visual = "操作演示", NeedVideo = true },
            new() { TimeRange = $"00:{d * 2:D2}-00:{d * 3:D2}", Scene = "操作2", Narration = "第二步，输入内容", Visual = "输入演示", NeedVideo = true },
            new() { TimeRange = $"00:{d * 3:D2}-00:{totalDuration:D2}", Scene = "结果", Narration = "看，这就完成了！", Visual = "结果展示", NeedImage = true }
        };
    }

    #endregion

    #region Helpers

    private static string EscapeJson(string text)
    {
        return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }

    private static async Task<bool> CheckHyperFramesAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "npx",
                Arguments = "hyperframes --version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task<string> RunHyperFramesAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "npx",
            Arguments = $"hyperframes render {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 HyperFrames");
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"HyperFrames 渲染失败：{error.Substring(0, Math.Min(500, error.Length))}");

        return output;
    }

    private static async Task<bool> CheckFfmpegAsync()
    {
        try
        {
            var psi = new ProcessStartInfo { FileName = "ffmpeg", Arguments = "-version", UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    private static async Task RunFfmpegAsync(string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo { FileName = "ffmpeg", Arguments = arguments, UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 FFmpeg");
        var error = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg 错误：{error.Substring(0, Math.Min(500, error.Length))}");
    }

    private static async Task<bool> GenerateSpeechAsync(string text, string outputPath, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "edge-tts",
                Arguments = $"--voice zh-CN-XiaoxiaoNeural --text \"{text}\" --write-media \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 edge-tts");
            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch { return false; }
    }

    private static ToolResult Fail(string error) => new() { Name = "short_video_maker", Success = false, Error = error };

    #endregion
}

internal class ScriptSegment
{
    public string TimeRange { get; set; } = "";
    public string Scene { get; set; } = "";
    public string Narration { get; set; } = "";
    public string Visual { get; set; } = "";
    public bool NeedImage { get; set; }
    public bool NeedVideo { get; set; }
}
