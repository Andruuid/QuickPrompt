[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$wixVersion = '4.0.6'
$toolDirectory = Join-Path $projectRoot '.tools\wix'
$wixExe = Join-Path $toolDirectory 'wix.exe'
$publishDirectory = Join-Path $projectRoot 'artifacts\installer-publish'
$outputDirectory = Join-Path $projectRoot 'artifacts\installer'
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'QuickPrompt.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
function Assert-Exit([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step failed with exit code $LASTEXITCODE." } }
Push-Location $projectRoot
try {
    if (-not (Test-Path -LiteralPath $wixExe)) {
        dotnet tool install wix --tool-path $toolDirectory --version $wixVersion
        Assert-Exit 'WiX install'
    }
    & $wixExe extension add "WixToolset.UI.wixext/$wixVersion"
    Assert-Exit 'WiX UI extension'
    dotnet publish QuickPrompt.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDirectory
    Assert-Exit 'Application publish'
    $nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget\packages' }
    $assets = Get-Content -LiteralPath 'obj\project.assets.json' -Raw | ConvertFrom-Json
    $runtimeKey = $assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'Microsoft.NETCore.App.Runtime.win-x64/*' } | Select-Object -First 1
    $desktopKey = $assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'Microsoft.WindowsDesktop.App.Runtime.win-x64/*' } | Select-Object -First 1
    # Runtime packs are recorded in the project's restore metadata, not always its library graph.
    if (-not $runtimeKey -or -not $desktopKey) {
        $runtimeConfig = Get-Content -LiteralPath 'bin\Release\net10.0-windows\win-x64\QuickPrompt.runtimeconfig.json' -Raw | ConvertFrom-Json
        $frameworks = $runtimeConfig.runtimeOptions.includedFrameworks
        $runtimeKey = 'microsoft.netcore.app.runtime.win-x64/' + ($frameworks | Where-Object name -eq 'Microsoft.NETCore.App').version
        $desktopKey = 'microsoft.windowsdesktop.app.runtime.win-x64/' + ($frameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App').version
    }
    Copy-Item -LiteralPath (Join-Path $nugetRoot "$runtimeKey/LICENSE.TXT") -Destination (Join-Path $publishDirectory 'DOTNET-LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $nugetRoot "$runtimeKey/THIRD-PARTY-NOTICES.TXT") -Destination (Join-Path $publishDirectory 'DOTNET-THIRD-PARTY-NOTICES.txt') -Force
    Copy-Item -LiteralPath (Join-Path $nugetRoot "$desktopKey/LICENSE") -Destination (Join-Path $publishDirectory 'WINDOWSDESKTOP-LICENSE.txt') -Force
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $msiPath = Join-Path $outputDirectory "QuickPrompt-$version-win-x64.msi"
    & $wixExe build installer\QuickPrompt.wxs -arch x64 -ext WixToolset.UI.wixext -culture en-us -loc installer\Strings.en-us.wxl -cc (Join-Path $outputDirectory 'cab-cache') -d "Root=$projectRoot" -d "PublishDir=$publishDirectory" -d "Version=$version" -o $msiPath
    Assert-Exit 'MSI build'
    Get-FileHash -LiteralPath $msiPath -Algorithm SHA256 | ForEach-Object { $_.Hash + '  ' + [IO.Path]::GetFileName($_.Path) } | Set-Content -LiteralPath ($msiPath + '.sha256') -Encoding ascii
    Write-Output "Installer: $msiPath"
} finally { Pop-Location }
