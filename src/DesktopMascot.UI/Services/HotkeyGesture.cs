namespace DesktopMascot.UI.Services;

public sealed class HotkeyGesture : IEquatable<HotkeyGesture>
{
    private static readonly Dictionary<string, string> KeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Esc"] = "Escape",
        ["Del"] = "Delete",
        ["PgUp"] = "PageUp",
        ["PgDn"] = "PageDown",
        ["Return"] = "Enter",
        ["Spacebar"] = "Space",
        [" "] = "Space"
    };

    private static readonly Dictionary<string, uint> VirtualKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Backspace"] = 0x08,
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Escape"] = 0x1B,
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E
    };

    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = "Space";

    public string DisplayText => ToDisplayText();

    public static HotkeyGesture DefaultChat() => new()
    {
        Control = true,
        Alt = true,
        Key = "Space"
    };

    public static HotkeyGesture DefaultScreenSelection() => new()
    {
        Control = true,
        Shift = true,
        Key = "S"
    };

    public HotkeyGesture Clone() => new()
    {
        Control = Control,
        Alt = Alt,
        Shift = Shift,
        Win = Win,
        Key = Key
    };

    public bool IsValid(out string error)
    {
        error = string.Empty;

        if (!Control && !Alt && !Win)
        {
            error = "快捷键至少需要包含 Ctrl、Alt 或 Win。";
            return false;
        }

        if (!TryNormalizeKey(Key, out var normalizedKey))
        {
            error = $"不支持的按键：{Key}";
            return false;
        }

        Key = normalizedKey;
        return true;
    }

    public bool TryGetVirtualKey(out uint virtualKey)
    {
        if (!TryNormalizeKey(Key, out var normalizedKey))
        {
            virtualKey = 0;
            return false;
        }

        if (normalizedKey.Length == 1)
        {
            var keyChar = normalizedKey[0];
            if (keyChar is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                virtualKey = keyChar;
                return true;
            }
        }

        if (normalizedKey.Length is >= 2 and <= 3 &&
            normalizedKey[0] == 'F' &&
            int.TryParse(normalizedKey[1..], out var functionKey) &&
            functionKey is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionKey - 1);
            return true;
        }

        return VirtualKeys.TryGetValue(normalizedKey, out virtualKey);
    }

    public static bool TryParse(string? text, out HotkeyGesture gesture, out string error)
    {
        gesture = new HotkeyGesture();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "快捷键不能为空。";
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            error = "快捷键不能为空。";
            return false;
        }

        string? key = null;
        var control = false;
        var alt = false;
        var shift = false;
        var win = false;

        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                if (control)
                {
                    error = "Ctrl 重复出现。";
                    return false;
                }

                control = true;
                continue;
            }

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                if (alt)
                {
                    error = "Alt 重复出现。";
                    return false;
                }

                alt = true;
                continue;
            }

            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                if (shift)
                {
                    error = "Shift 重复出现。";
                    return false;
                }

                shift = true;
                continue;
            }

            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Meta", StringComparison.OrdinalIgnoreCase))
            {
                if (win)
                {
                    error = "Win 重复出现。";
                    return false;
                }

                win = true;
                continue;
            }

            if (key is not null)
            {
                error = "快捷键只能包含一个主按键。";
                return false;
            }

            key = part;
        }

        if (key is null)
        {
            error = "缺少主按键。";
            return false;
        }

        if (!TryNormalizeKey(key, out var normalizedKey))
        {
            error = $"不支持的按键：{key}";
            return false;
        }

        gesture = new HotkeyGesture
        {
            Control = control,
            Alt = alt,
            Shift = shift,
            Win = win,
            Key = normalizedKey
        };

        return gesture.IsValid(out error);
    }

    public bool Equals(HotkeyGesture? other)
    {
        if (other is null)
            return false;

        return Control == other.Control &&
               Alt == other.Alt &&
               Shift == other.Shift &&
               Win == other.Win &&
               string.Equals(NormalizedKey(), other.NormalizedKey(), StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is HotkeyGesture other && Equals(other);

    public override int GetHashCode()
    {
        return HashCode.Combine(Control, Alt, Shift, Win, NormalizedKey().ToUpperInvariant());
    }

    private string ToDisplayText()
    {
        var parts = new List<string>(5);
        if (Control)
        {
            parts.Add("Ctrl");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Win)
        {
            parts.Add("Win");
        }

        parts.Add(NormalizedKey());
        return string.Join("+", parts);
    }

    private string NormalizedKey()
    {
        return TryNormalizeKey(Key, out var normalizedKey) ? normalizedKey : Key.Trim();
    }

    private static bool TryNormalizeKey(string? rawKey, out string normalizedKey)
    {
        normalizedKey = string.Empty;

        if (string.IsNullOrWhiteSpace(rawKey))
            return false;

        var key = rawKey.Trim();
        if (KeyAliases.TryGetValue(key, out var alias))
        {
            key = alias;
        }

        if (key.Length == 1)
        {
            var keyChar = char.ToUpperInvariant(key[0]);
            if (keyChar is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                normalizedKey = keyChar.ToString();
                return true;
            }
        }

        if (key.Length is >= 2 and <= 3 &&
            char.ToUpperInvariant(key[0]) == 'F' &&
            int.TryParse(key[1..], out var functionKey) &&
            functionKey is >= 1 and <= 24)
        {
            normalizedKey = $"F{functionKey}";
            return true;
        }

        if (VirtualKeys.ContainsKey(key))
        {
            normalizedKey = VirtualKeys.Keys.First(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
            return true;
        }

        return false;
    }
}
