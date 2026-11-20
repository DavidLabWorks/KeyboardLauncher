using System.Text.Json;

namespace KeyboardLauncher;

internal sealed class SearchHistory(string file)
{
    private List<string>? entries;
    internal IReadOnlyList<string> Entries => entries ??= Read();

    private List<string> Read()
    {
        try { return JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file))?.Where(p => !string.IsNullOrWhiteSpace(p)).Take(20).ToList() ?? []; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }

    internal void Record(string path)
    {
        entries = Entries.Prepend(path).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(entries));
            File.Move(file + ".tmp", file, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { System.Diagnostics.Debug.WriteLine(ex); }
    }
}
