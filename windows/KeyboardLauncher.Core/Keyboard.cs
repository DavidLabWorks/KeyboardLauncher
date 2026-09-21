namespace KeyboardLauncher.Core;

public static class KeyboardLayout
{
    public static readonly string[] Rows = ["1234567890-=", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM"];
    public static readonly string Keys = string.Concat(Rows);
    public const int Count = 38;
    // ANSI scan codes preserve physical positions across keyboard languages.
    public static readonly int[] ScanCodes = [2,3,4,5,6,7,8,9,10,11,12,13,16,17,18,19,20,21,22,23,24,25,30,31,32,33,34,35,36,37,38,44,45,46,47,48,49,50];
    public static int Slot(int scanCode, int page)
    {
        var index = Array.IndexOf(ScanCodes, scanCode);
        return page < 0 || index < 0 ? -1 : page * Count + index;
    }
}

public readonly record struct Hotkey(uint Modifiers, uint Key)
{
    public string ToShortcutString()
    {
        var parts = new List<string>();
        if ((Modifiers & 2) != 0) parts.Add("ctrl");
        if ((Modifiers & 1) != 0) parts.Add("alt");
        if ((Modifiers & 4) != 0) parts.Add("shift");
        if ((Modifiers & 8) != 0) parts.Add("win");
        parts.Add(Key switch
        {
            >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)Key).ToString().ToLowerInvariant(),
            >= 0x70 and <= 0x87 => $"f{Key - 0x70 + 1}",
            0x20 => "space", 0x0D => "enter", 9 => "tab", 0x1B => "esc", 8 => "backspace",
            0x2E => "delete", 0x2D => "insert", 0x24 => "home", 0x23 => "end", 0x21 => "pageup",
            0x22 => "pagedown", 0x25 => "left", 0x26 => "up", 0x27 => "right", 0x28 => "down",
            0xBD => "-", 0xBB => "=", 0xDB => "[", 0xDD => "]", 0xBA => ";", 0xDE => "'",
            0xBC => ",", 0xBE => ".", 0xBF => "/", 0xDC => ((char)92).ToString(), 0xC0 => "`",
            _ => throw new FormatException("暂不支持这个按键，请录入其他组合。")
        });
        return string.Join("+", parts);
    }

    public string ToDisplayString() => string.Join(" + ", ToShortcutString().Split('+').Select(part => part switch
    {
        "pageup" => "PageUp", "pagedown" => "PageDown",
        _ => char.ToUpperInvariant(part[0]) + part[1..]
    }));

    public static Hotkey Parse(string value, bool requireModifier = true)
    {
        uint modifiers = 0, key = 0;
        foreach (var token in value.ToLowerInvariant().Split('+').Select(x => x.Trim()))
        {
            uint mod = token switch { "ctrl" or "control" => 2, "alt" => 1, "shift" => 4, "win" or "windows" => 8, _ => 0 };
            if (mod != 0) { if ((modifiers & mod) != 0) throw new FormatException("修饰键重复。"); modifiers |= mod; continue; }
            if (key != 0) throw new FormatException("快捷键只能包含一个普通按键。");
            key = token switch
            {
                "space" => 0x20, "enter" or "return" => 0x0D, "tab" => 9, "escape" or "esc" => 0x1B,
                "backspace" => 8, "delete" => 0x2E, "insert" => 0x2D, "home" => 0x24, "end" => 0x23,
                "pageup" => 0x21, "pagedown" => 0x22, "left" => 0x25, "up" => 0x26, "right" => 0x27, "down" => 0x28,
                "-" => 0xBD, "=" => 0xBB, "[" => 0xDB, "]" => 0xDD, ";" => 0xBA, "'" => 0xDE,
                "," => 0xBC, "." => 0xBE, "/" => 0xBF, "\\" => 0xDC, "`" => 0xC0,
                _ when token.Length == 1 && char.IsAsciiLetterOrDigit(token[0]) => char.ToUpperInvariant(token[0]),
                _ when token.StartsWith('f') && int.TryParse(token[1..], out var f) && f is >= 1 and <= 24 => (uint)(0x70 + f - 1),
                _ => throw new FormatException($"无法识别按键：{token}。Windows 键请使用 win，Control 请使用 ctrl。")
            };
        }
        if (key == 0 || (requireModifier && modifiers == 0)) throw new FormatException("唤起快捷键需要修饰键和一个普通按键。");
        return new(modifiers, key);
    }
}

public sealed class DoubleModifierTap
{
    private string? modifier = "control";
    public string? Modifier { get => modifier; set { modifier = value; Reset(); } }
    private long? pressedAt, releasedAt;
    private uint pressedKey;
    public void Reset() { pressedAt = releasedAt = null; pressedKey = 0; }
    public bool Handle(uint key, bool down, long time, bool repeat, bool otherModifiers)
    {
        bool selected = modifier switch { "control" => key is 0xA2 or 0xA3, "alt" => key is 0xA4 or 0xA5, "shift" => key is 0xA0 or 0xA1, "leftControl" => key == 0xA2, "rightControl" => key == 0xA3, "leftAlt" => key == 0xA4, "rightAlt" => key == 0xA5, "leftShift" => key == 0xA0, "rightShift" => key == 0xA1, _ => false };
        if (!selected || repeat || otherModifiers) { Reset(); return false; }
        if (down)
        {
            if (pressedAt != null) { Reset(); return false; }
            if (pressedKey != 0 && pressedKey != key) Reset();
            pressedKey = key; pressedAt = time; return false;
        }
        if (pressedAt is not long start || pressedKey != key || time - start > 300) { Reset(); return false; }
        pressedAt = null;
        if (releasedAt is long release && time - release <= 400) { Reset(); return true; }
        releasedAt = time;
        return false;
    }
}
