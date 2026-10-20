using System.Runtime.InteropServices;
using System.Text.Json;
using KeyboardLauncher.Core;

namespace KeyboardLauncher;

internal static class StartMenuLocalization
{
    private const int DisplayNameId = 101;
    private static readonly string[] Names = ["Keyboard Launcher", "KeyboardLauncher", "指间"];

    // Localize the display name; leave physical paths, targets, and uninstall
    // identity unchanged. Only this user's existing links to our fixed install
    // path qualify. MSIX names are resolved from Resources.resw by Windows.
    internal static string Apply()
    {
        var systemLanguage = System.Globalization.CultureInfo.GetCultureInfo(GetUserDefaultUILanguage()).Name;
        var module = Path.Combine(AppContext.BaseDirectory, "Assets", LocalInstallation.ShellResourceFileName(systemLanguage));
        if (!File.Exists(module)) throw new FileNotFoundException("Missing shell display name resources.", module);
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var folders = new[] { programs }.Concat(Names.Select(name => Path.Combine(programs, name)));
        var updated = new List<string>();
        object? shell = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!);
            foreach (var folder in folders.Where(Directory.Exists))
            {
                bool ownsFolder = false;
                foreach (var name in Names)
                {
                    var path = Path.Combine(folder, name + ".lnk");
                    if (!File.Exists(path)) continue;
                    object? link = null;
                    try
                    {
                        link = ((dynamic)shell!).CreateShortcut(path);
                        string target = Environment.ExpandEnvironmentVariables((string)((dynamic)link).TargetPath);
                        if (!LocalInstallation.IsCanonicalExecutable(target)) continue;
                        Marshal.ThrowExceptionForHR(SHSetLocalizedName(path, module, DisplayNameId));
                        // Wake the Start app indexer as well as Explorer: the
                        // localized name itself lives in desktop.ini.
                        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                        SHChangeNotify(0x2000, 0x0005, path, 0); // UPDATEITEM, PATHW
                        updated.Add(path); ownsFolder = true;
                    }
                    finally { if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link); }
                }
                if (ownsFolder && !string.Equals(folder, programs, StringComparison.OrdinalIgnoreCase))
                {
                    Marshal.ThrowExceptionForHR(SHSetLocalizedName(folder, module, DisplayNameId));
                    SHChangeNotify(0x2000, 0x0005, folder, 0);
                }
            }
        }
        finally { if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
        return JsonSerializer.Serialize(new { SystemLanguage = systemLanguage, Updated = updated, ResourceModule = module, ResourceId = DisplayNameId });
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHSetLocalizedName(string path, string module, int resourceId);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint eventId, uint flags, string item, nint item2);
}
