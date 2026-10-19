param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskDotnet = Join-Path $taskRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $taskDotnet)) { $taskDotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$taskOutput = Join-Path $taskRoot 'dist\windows-x64'
& $taskDotnet publish (Join-Path $taskRoot 'src\PKForge.App\PKForge.App.csproj') `
    -f net10.0-windows10.0.19041.0 -c $Configuration -r win-x64 --self-contained true `
    -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:PublishTrimmed=false `
    -o $taskOutput --nologo
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE'),(Join-Path $taskRoot 'README.md'),(Join-Path $taskRoot 'README.ru.md'),(Join-Path $taskRoot 'src\PKForge.App\Resources\Fonts\NotoSans-OFL.txt') -Destination $taskOutput
Write-Host "Ready: $taskOutput\PKForge.App.exe"
