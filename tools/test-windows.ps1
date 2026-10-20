param(
    [ValidateSet('events', 'park', 'theme', 'stats', 'gifts', 'music', 'storage')]
    [string[]]$Mode = @('events', 'park', 'theme', 'stats', 'gifts', 'music'),
    [string]$SavePath,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $repo '.tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet).Source }
if (!$SavePath) { $SavePath = Join-Path $repo 'save\main' }
if (($Mode -contains 'stats' -or $Mode -contains 'gifts' -or $Mode -contains 'storage') -and !(Test-Path -LiteralPath $SavePath)) {
    throw 'Supply -SavePath with a Gen 6 save. Tests edit a copy only.'
}
if (!$SkipBuild) {
    & $dotnet build (Join-Path $repo 'src\PKForge.App\PKForge.App.csproj') -c Debug -p:DiagnosticBuild=true -p:PublishReadyToRun=false
    if ($LASTEXITCODE) { throw 'Diagnostic build failed.' }
}
$app = Join-Path $repo 'src\PKForge.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\PKForge.Diagnostics.exe'
$run = Join-Path $repo ('.tools\smoke\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
$audio = Join-Path $run 'music #1.wav'
$writer = [IO.BinaryWriter]::new([IO.File]::Create($audio))
try {
    $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
    $writer.Write([int](36 + 960000))
    $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
    $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]1)
    $writer.Write([int]16000); $writer.Write([int]32000)
    $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
    $writer.Write([int]960000); $writer.Write([byte[]]::new(960000))
} finally { $writer.Dispose() }
$previous = @{}
foreach ($name in @('PKFORGE_SMOKE', 'PKFORGE_SMOKE_OUTPUT', 'PKFORGE_SMOKE_SAVE', 'PKFORGE_SMOKE_AUDIO')) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    $env:PKFORGE_SMOKE_SAVE = [IO.Path]::GetFullPath($SavePath)
    $env:PKFORGE_SMOKE_AUDIO = $audio
    foreach ($scenario in $Mode) {
        $env:PKFORGE_SMOKE = $scenario
        $env:PKFORGE_SMOKE_OUTPUT = Join-Path $run $scenario
        $process = Start-Process -FilePath $app -WorkingDirectory $repo -WindowStyle Hidden -PassThru
        if (!$process.WaitForExit(60000)) {
            Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
            throw "Timed out: $scenario"
        }
        $log = Get-Content -LiteralPath (Join-Path $env:PKFORGE_SMOKE_OUTPUT 'smoke.log') -Raw -Encoding UTF8
        if ($log -match '\bFAIL\b' -or $log -notmatch '\bPASS\b') { throw $log }
        Write-Output "PASS $scenario"
    }
    Write-Output "Logs and window images: $run"
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
