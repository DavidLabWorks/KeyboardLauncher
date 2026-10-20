param(
    [Parameter(Mandatory = $true)][string]$ResourceCompiler,
    [Parameter(Mandatory = $true)][string]$Linker,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'KeyboardLauncher/Assets')
)
$ErrorActionPreference = 'Stop'
# Resource-only x86 images are loaded as data, on both x64 and ARM64.
# Keep separate neutral modules: ordinary desktop DLL string loading otherwise
# falls back to a different language before the user's preferred UI language.
$staging = Join-Path ([IO.Path]::GetTempPath()) ('KeyboardLauncher-shell-resources-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
foreach ($language in @('en-US', 'zh-CN')) {
    [xml]$resources = Get-Content -LiteralPath (Join-Path $PSScriptRoot "KeyboardLauncher/Strings/$language/Resources.resw") -Raw -Encoding UTF8
    $displayName = ($resources.root.data | Where-Object name -eq 'AppDisplayName').value.Replace('"', '""')
    $resourceText = "#pragma code_page(65001)`nLANGUAGE 0, 0`nSTRINGTABLE`nBEGIN`n101 ""$displayName""`nEND`n"
    $resourceScript = Join-Path $staging "$language.rc"
    $compiledResources = Join-Path $staging "$language.res"
    [IO.File]::WriteAllText($resourceScript, $resourceText, [Text.UTF8Encoding]::new($false))
    & $ResourceCompiler /nologo /fo $compiledResources $resourceScript
    if ($LASTEXITCODE -ne 0) { throw 'Shell string resource compilation failed.' }
    $moduleName = if ($language -eq 'zh-CN') { 'ShellNamesChinese.dll' } else { 'ShellNamesEnglish.dll' }
    $modulePath = [IO.Path]::GetFullPath((Join-Path $OutputDirectory $moduleName))
    & $Linker /NOLOGO /DLL /NOENTRY /NODEFAULTLIB /MACHINE:X86 /BREPRO ('/OUT:' + $modulePath) $compiledResources
    if ($LASTEXITCODE -ne 0) { throw 'Shell resource DLL linking failed.' }
    Write-Output "Generated shell name resources: $modulePath"
}
