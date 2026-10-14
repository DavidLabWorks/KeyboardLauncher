param(
    [ValidateSet('ARM64', 'x64')][string]$Architecture = 'ARM64',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts/store'),
    [string]$IdentityFile = (Join-Path $PSScriptRoot 'store/identity.json'),
    [string]$MakeAppx,
    [switch]$Preview
)
$ErrorActionPreference = 'Stop'
$config = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../app.json') -Encoding UTF8 -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($config.name) -or $config.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid app.json name or version.' }
$version = [Version]($config.version + '.0')
if ($version.Major -eq 0 -or $version.Major -gt 65535 -or $version.Minor -gt 65535 -or $version.Build -gt 65535) { throw 'MSIX version components must be 0..65535, with a nonzero major version.' }
if ($Preview) {
    $identity = @{ name = 'KeyboardLauncher.LocalPreview'; publisher = 'CN=KeyboardLauncher Local Preview'; publisherDisplayName = 'Local Preview' }
} else {
    if (!(Test-Path -LiteralPath $IdentityFile)) { throw 'Copy store/identity.example.json to store/identity.json and enter the Partner Center identity, or use -Preview for local validation.' }
    $identity = Get-Content -LiteralPath $IdentityFile -Encoding UTF8 -Raw | ConvertFrom-Json
    foreach ($field in @('name', 'publisher', 'publisherDisplayName')) {
        if ([string]::IsNullOrWhiteSpace($identity.$field)) { throw "Missing Partner Center identity field: $field" }
    }
    if ($identity.name -eq 'KeyboardLauncher.LocalPreview') { throw 'The local preview identity cannot be submitted to the Store.' }
}
# Use the same Windows SDK version pinned by the project; a full SDK installation is also supported.
if (!$MakeAppx) {
    $nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $candidates = @(
        (Join-Path $nuget 'microsoft.windows.sdk.buildtools/10.0.26100.4654/bin/10.0.26100.0/x64/makeappx.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin/10.0.26100.0/x64/makeappx.exe')
    )
    $MakeAppx = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$MakeAppx -or !(Test-Path -LiteralPath $MakeAppx)) { throw 'Install the Windows SDK or pass -MakeAppx with its makeappx.exe path.' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
# A fresh local staging directory avoids stale files and Windows SDK UNC path limitations.
$staging = Join-Path ([IO.Path]::GetTempPath()) ('KeyboardLauncher-msix-' + [Guid]::NewGuid().ToString('N'))
$appDirectory = Join-Path $staging 'app'
try {
    New-Item -ItemType Directory -Force $staging | Out-Null
    $project = Join-Path $PSScriptRoot 'KeyboardLauncher/KeyboardLauncher.csproj'
    dotnet publish $project -c Release "-p:Platform=$Architecture" -p:StoreBuild=true --artifacts-path (Join-Path $staging 'build') --output $appDirectory --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Store release publish failed.' }
    Get-ChildItem -LiteralPath $appDirectory -Filter '*.pdb' -Recurse | Remove-Item
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'store/AppxManifest.xml') -Encoding UTF8 -Raw
    $manifest.Package.Identity.SetAttribute('Name', $identity.name)
    $manifest.Package.Identity.SetAttribute('Publisher', $identity.publisher)
    $manifest.Package.Identity.SetAttribute('Version', $version.ToString())
    $manifest.Package.Identity.SetAttribute('ProcessorArchitecture', $Architecture.ToLowerInvariant())
    $manifest.Package.Properties.DisplayName = 'ms-resource:AppDisplayName'
    $manifest.Package.Properties.PublisherDisplayName = $identity.publisherDisplayName
    $application = $manifest.Package.Applications.Application
    $application.VisualElements.SetAttribute('DisplayName', 'ms-resource:AppDisplayName')
    $application.VisualElements.SetAttribute('Description', 'ms-resource:AppDisplayName')
    $application.Extensions.Extension.StartupTask.SetAttribute('DisplayName', 'ms-resource:AppDisplayName')
    $manifest.Save((Join-Path $appDirectory 'AppxManifest.xml'))
    Add-Type -AssemblyName System.Drawing
    $source = [Drawing.Image]::FromFile((Join-Path $appDirectory 'Assets/AppIcon.png'))
    try {
        foreach ($asset in @(@('StoreLogo', 50), @('Square44x44Logo', 44), @('Square150x150Logo', 150))) {
            $bitmap = New-Object Drawing.Bitmap ([int]$asset[1]), ([int]$asset[1])
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.DrawImage($source, 0, 0, $bitmap.Width, $bitmap.Height)
                $bitmap.Save((Join-Path $appDirectory ('Assets/' + $asset[0] + '.png')), [Drawing.Imaging.ImageFormat]::Png)
            } finally { $graphics.Dispose(); $bitmap.Dispose() }
        }
    } finally { $source.Dispose() }
    # Index manifest strings separately from framework resources, using the Store package identity.
    $makePri = Join-Path (Split-Path $MakeAppx) 'makepri.exe'
    if (!(Test-Path -LiteralPath $makePri)) { throw 'Windows SDK makepri.exe is required for localized package names.' }
    $resourceDirectory = Join-Path $staging 'resources'
    New-Item -ItemType Directory -Force $resourceDirectory | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'KeyboardLauncher/Strings') $resourceDirectory -Recurse
    $priConfig = Join-Path $staging 'priconfig.xml'
    & $makePri createconfig /cf $priConfig /dq en-US /o
    if ($LASTEXITCODE -ne 0) { throw 'Resource index configuration failed.' }
    [xml]$priSettings = Get-Content -LiteralPath $priConfig
    # Ship both languages in this MSIX instead of generating separate language packs.
    $priSettings.resources.RemoveChild($priSettings.resources.packaging) | Out-Null
    $priSettings.Save($priConfig)
    & $makePri new /pr $resourceDirectory /cf $priConfig /in $identity.name /of (Join-Path $appDirectory 'resources.pri') /o
    if ($LASTEXITCODE -ne 0) { throw 'Localized resource index generation failed.' }
    $suffix = if ($Preview) { '-preview' } else { '' }
    $fileName = "KeyboardLauncher-$version-$($Architecture.ToLowerInvariant())$suffix.msix"
    $package = Join-Path $staging $fileName
    & $MakeAppx pack /d $appDirectory /p $package /o
    if ($LASTEXITCODE -ne 0) { throw 'MSIX validation or packaging failed.' }
    Copy-Item -LiteralPath $package -Destination $OutputDirectory -Force
    $hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ($fileName + '.sha256')), "$hash  $fileName`r`n")
    Write-Host "Created unsigned MSIX: $(Join-Path $OutputDirectory $fileName)"
    if ($Preview) { Write-Warning 'Local preview identity only. Not ready for Store submission or real trial/purchase testing.' }
} finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
