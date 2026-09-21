using System.Globalization;
using System.Text.Json;

namespace KeyboardLauncher.Core;

public static class L
{
    private static readonly IReadOnlyDictionary<string, string> English = LoadEnglish();
    public static string Language { get; set; } = "en";
    public static string T(string text) => Language == "en" && English.TryGetValue(text, out var value) ? value : text;
    public static string F(string text, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(text), args);
    private static IReadOnlyDictionary<string, string> LoadEnglish()
    {
        using var stream = typeof(L).Assembly.GetManifestResourceStream("KeyboardLauncher.Core.Strings.en.json")
            ?? throw new InvalidOperationException("Missing English localization resources.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}