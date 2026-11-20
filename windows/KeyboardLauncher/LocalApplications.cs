namespace KeyboardLauncher;

internal sealed class AppEntry(string name, string path, DateTimeOffset? added = null) : System.ComponentModel.INotifyPropertyChanged
{
    public string Name { get; } = name;
    public string Path { get; } = path;
    public DateTimeOffset? Added { get; } = added;
    private bool loading;
    private Microsoft.UI.Xaml.Media.ImageSource? icon;
    public Microsoft.UI.Xaml.Media.ImageSource? Icon
    {
        get { if (!loading) { loading = true; _ = LoadIcon(); } return icon; }
    }
    private async Task LoadIcon()
    {
        icon = await ApplicationIcons.Load(Path);
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Icon)));
    }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => Name;
}

internal static class LocalApplications
{
    internal static List<AppEntry> Scan()
    {
        var apps = ScanStartMenu();
        var nonAppTargets = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var installed = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var package in new Windows.Management.Deployment.PackageManager().FindPackagesForUser(""))
                installed[package.Id.FamilyName] = package.InstalledDate;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine(ex); }
        // Packaged apps can be registered in AppsFolder without a Start menu shortcut.
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType != null)
        {
            try
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic folder = shell.NameSpace("shell:AppsFolder");
                if (folder != null)
                    foreach (dynamic item in folder.Items())
                    {
                        string id = item.Path;
                        if (Path.IsPathRooted(id) && (Directory.Exists(id) || Path.GetExtension(id).ToLowerInvariant() is
                            ".url" or ".chm" or ".htm" or ".html" or ".txt" or ".pdf"))
                            nonAppTargets.Add((string)item.Name);
                        if (!Path.IsPathRooted(id) && id.Contains('!'))
                            apps.Add(new AppEntry((string)item.Name, @"shell:AppsFolder\" + id,
                                installed.TryGetValue(id.Split('!')[0], out var date) ? date : null));
                    }
            }
            catch (System.Runtime.InteropServices.COMException) { }
        }
        return apps.Where(a => !string.IsNullOrWhiteSpace(a.Name) && !nonAppTargets.Contains(a.Name))
            .DistinctBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    internal static List<AppEntry> ScanStartMenu()
    {
        var result = new List<AppEntry>();
        foreach (var folder in new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu })
        {
            var directory = Environment.GetFolderPath(folder);
            if (!Directory.Exists(directory)) continue;
            try
            {
                result.AddRange(new DirectoryInfo(directory).EnumerateFiles("*.lnk", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                    .Select(file => new AppEntry(Path.GetFileNameWithoutExtension(file.Name), file.FullName, file.CreationTimeUtc)));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return result.DistinctBy(x => x.Name).OrderBy(x => x.Name).ToList();
    }
}
