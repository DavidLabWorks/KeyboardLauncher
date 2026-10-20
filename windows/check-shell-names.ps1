param([string]$ResourceDirectory = (Join-Path $PSScriptRoot 'KeyboardLauncher/Assets'))
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class ShellNameResourceCheck {
    [DllImport("shlwapi.dll", CharSet=CharSet.Unicode)]
    static extern int SHLoadIndirectString(string source, StringBuilder output, uint size, IntPtr reserved);
    public static string Resolve(string module) {
        var output = new StringBuilder(256);
        Marshal.ThrowExceptionForHR(SHLoadIndirectString("@" + module + ",-101", output, 256, IntPtr.Zero));
        return output.ToString();
    }
}
'@
[xml]$english = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'KeyboardLauncher/Strings/en-US/Resources.resw') -Raw -Encoding UTF8
[xml]$chinese = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'KeyboardLauncher/Strings/zh-CN/Resources.resw') -Raw -Encoding UTF8
$englishName = ($english.root.data | Where-Object name -eq 'AppDisplayName').value
$chineseName = ($chinese.root.data | Where-Object name -eq 'AppDisplayName').value
foreach ($test in @(@('zh-CN', $chineseName), @('en-US', $englishName), @('zh-TW', $chineseName), @('fr-FR', $englishName))) {
    $moduleName = if ($test[0].StartsWith('zh-')) { 'ShellNamesChinese.dll' } else { 'ShellNamesEnglish.dll' }
    $actual = [ShellNameResourceCheck]::Resolve([IO.Path]::GetFullPath((Join-Path $ResourceDirectory $moduleName)))
    if ($actual -cne $test[1]) { throw "Shell name mismatch for $($test[0]): expected '$($test[1])', got '$actual'." }
    Write-Output "PASS $($test[0]): $actual"
}
