using System.Diagnostics;

namespace KeyboardLauncher.Core;

public static class ScriptAction
{
    public static bool IsSupported(string path) => Path.IsPathFullyQualified(path) &&
        new[] { ".cmd", ".bat", ".ps1" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static ProcessStartInfo CreateStartInfo(string path)
    {
        if (!IsSupported(path)) throw new FormatException(L.T("请选择 .cmd、.bat 或 .ps1 脚本文件。"));
        if (!File.Exists(path)) throw new FileNotFoundException(L.T("脚本文件不存在，请重新选择。"), path);
        var start = new ProcessStartInfo { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path)! };
        if (Path.GetExtension(path).Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            start.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-File"); start.ArgumentList.Add(path);
        }
        else
        {
            start.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            // Expand once inside quotes so spaces, &, and literal % in file names remain part of the path.
            start.Environment["KEYBOARD_LAUNCHER_SCRIPT"] = path;
            start.Arguments = "/d /v:off /s /c \"\"%KEYBOARD_LAUNCHER_SCRIPT%\"\"";
        }
        return start;
    }
}