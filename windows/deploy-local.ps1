param(
    [Parameter(Mandatory = $true)][string]$SourceDirectory,
    [ValidatePattern('^--[a-z0-9-]+$')][string[]]$LaunchArguments = @()
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SourceDirectory).ProviderPath.TrimEnd('\')
$destination = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\KeyboardLauncher'))
$executable = Join-Path $destination 'KeyboardLauncher.exe'
if ($source.Equals($destination, [StringComparison]::OrdinalIgnoreCase)) { throw 'Choose a build output directory, not the installed directory.' }
foreach ($required in @('KeyboardLauncher.exe', 'KeyboardLauncher.dll', 'KeyboardLauncher.Core.dll', 'KeyboardLauncher.runtimeconfig.json', 'Assets\AppIcon.ico')) {
    if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) { throw "Incomplete build output: $required" }
}

# Serialise deployment across separate terminals and build configurations.
$deploymentMutex = New-Object Threading.Mutex($false, ('Local\KeyboardLauncher.Deploy.' + $env:USERNAME))
$ownsMutex = $false
try {
    try { $ownsMutex = $deploymentMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $ownsMutex = $true }
    if (-not $ownsMutex) { throw 'Another Keyboard Launcher deployment is running.' }
    $running = @(Get-Process -Name KeyboardLauncher -ErrorAction SilentlyContinue)
    foreach ($process in $running) {
        if (-not $process.Path -or -not $process.Path.Equals($executable, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Close the Keyboard Launcher running outside the fixed install directory first.'
        }
    }
    foreach ($process in $running) {
        Stop-Process -Id $process.Id
        if (-not $process.WaitForExit(5000)) { throw 'The previous instance did not exit.' }
    }
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    # Copy only build output. Config remains in LocalAppData\KeyboardLauncher;
    # the installer's Uninstall.exe and registration are also preserved.
    Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
        if ($_.Name -notin @('config.json', 'config.json.bak')) {
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
        }
    }
    $runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    $existingStartup = Get-ItemProperty -LiteralPath $runKey -Name KeyboardLauncher -ErrorAction SilentlyContinue
    if ($null -ne $existingStartup) {
        Set-ItemProperty -LiteralPath $runKey -Name KeyboardLauncher -Value ('"' + $executable + '" --background')
    }
    if ($LaunchArguments.Count) { Start-Process -FilePath $executable -ArgumentList $LaunchArguments -WindowStyle Hidden }
    else { Start-Process -FilePath $executable -WindowStyle Hidden }
    Write-Output "Deployed and launched: $executable"
} finally {
    if ($ownsMutex) { $deploymentMutex.ReleaseMutex() }
    $deploymentMutex.Dispose()
}
