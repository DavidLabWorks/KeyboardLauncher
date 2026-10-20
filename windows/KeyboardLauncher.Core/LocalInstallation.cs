namespace KeyboardLauncher.Core;

public static class LocalInstallation
{
    // Stable across local debug/release builds, versions and CPU architectures.
    // Do not regenerate: Windows persists notification-area preferences by identity.
    public static readonly Guid TrayIconGuid = new("1fdf1ec1-68d7-4f1e-8279-091a7cc1a2d7");
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "KeyboardLauncher");
    public static string ExecutablePath => Path.Combine(DirectoryPath, "KeyboardLauncher.exe");
    public static string StartupCommand => $"\"{ExecutablePath}\" --background";

    public static string ShellResourceFileName(string? systemLanguage)
        => systemLanguage?.Split('-')[0].Equals("zh", StringComparison.OrdinalIgnoreCase) == true ? "ShellNamesChinese.dll" : "ShellNamesEnglish.dll";

    public static bool IsCanonicalExecutable(string? executable)
        => !string.IsNullOrWhiteSpace(executable) &&
            string.Equals(Path.GetFullPath(executable), ExecutablePath, StringComparison.OrdinalIgnoreCase);
}
