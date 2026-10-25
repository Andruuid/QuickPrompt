[CmdletBinding()]
param([string]$MsiPath)
$ErrorActionPreference = 'Stop'
[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\QuickPrompt.csproj') -Raw
$expectedVersion = [string]$project.Project.PropertyGroup.Version
if (-not $MsiPath) { $MsiPath = Join-Path $PSScriptRoot "..\artifacts\installer\QuickPrompt-$expectedVersion-win-x64.msi" }
$msiPath = (Resolve-Path -LiteralPath $MsiPath).Path
$windowsInstaller = New-Object -ComObject WindowsInstaller.Installer
$database = $windowsInstaller.OpenDatabase($msiPath, 0)
$view = $database.OpenView('SELECT `Value` FROM `Property` WHERE `Property` = ''ProductCode''')
$view.Execute()
$record = $view.Fetch()
$productCode = $record.StringData(1)
$registeredProductName = $windowsInstaller.ProductInfo($productCode, 'ProductName')
$view.Close()
foreach ($comObject in @($record, $view, $database, $windowsInstaller)) { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject) | Out-Null }

$installFolder = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\QuickPrompt'
$exe = Join-Path $installFolder 'QuickPrompt.exe'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'QuickPrompt.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$startupShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Startup')) 'QuickPrompt (Autostart).lnk'))
$shellApp = New-Object -ComObject Shell.Application
$folder = $shellApp.NameSpace([Environment]::GetFolderPath('Programs'))
$item = $folder.ParseName('QuickPrompt.lnk')
$checks = [ordered]@{
    ExecutableInstalled = Test-Path -LiteralPath $exe
    CorrectVersion = ((Get-Item -LiteralPath $exe).VersionInfo.ProductVersion -eq $expectedVersion)
    StartMenuTarget = ($shortcut.TargetPath -eq $exe)
    StartMenuShowsWindow = ($shortcut.Arguments -eq '')
    StartMenuAppId = ($item.ExtendedProperty('System.AppUserModel.ID') -eq 'QuickPrompt.Desktop')
    RegisteredApplication = ((Get-ItemPropertyValue -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\QuickPrompt.exe' -Name '(default)') -eq $exe)
    HiddenStartup = ($startupShortcut.TargetPath -eq $exe -and $startupShortcut.Arguments -eq '--hidden')
    InstallerRegistration = ($registeredProductName -eq 'QuickPrompt')
    RuntimeNotices = ((Test-Path -LiteralPath (Join-Path $installFolder 'DOTNET-THIRD-PARTY-NOTICES.txt')) -and (Test-Path -LiteralPath (Join-Path $installFolder 'WINDOWSDESKTOP-LICENSE.txt')))
}
foreach ($comObject in @($item, $folder, $shellApp, $shortcut, $startupShortcut, $shell)) { [Runtime.InteropServices.Marshal]::FinalReleaseComObject($comObject) | Out-Null }
$checks.GetEnumerator() | ForEach-Object {
    if (-not $_.Value) { throw "FAIL: $($_.Key)" }
    Write-Output "PASS: $($_.Key)"
}
Write-Output "ProductCode: $productCode"
Get-StartApps | Where-Object Name -eq 'QuickPrompt' | Format-List
