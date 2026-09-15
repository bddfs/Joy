param(
    [ValidateSet('Build', 'Test', 'Run', 'Import')]
    [string]$Action = 'Run'
)

$ErrorActionPreference = 'Stop'
$joyRepoPath = Split-Path -Parent $PSScriptRoot
$joyWorkspacePath = Split-Path -Parent $joyRepoPath
$joyPortableDotnet = Join-Path $joyWorkspacePath 'tools\dotnet'
$joyPortableGodot = Join-Path $joyWorkspacePath 'tools\godot-4.7.2\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'

if (Test-Path -LiteralPath (Join-Path $joyPortableDotnet 'dotnet.exe')) {
    $env:DOTNET_ROOT = $joyPortableDotnet
    $env:DOTNET_ROOT_X64 = $joyPortableDotnet
    $env:PATH = "$joyPortableDotnet;$env:PATH"
}
$joyDotnetCommand = Get-Command dotnet -ErrorAction Stop
Set-Location -LiteralPath $joyRepoPath

if ($Action -eq 'Test') {
    & $joyDotnetCommand.Source run --project (Join-Path $joyRepoPath 'Tests\Joy.Tests.csproj')
    exit $LASTEXITCODE
}
if ($Action -in @('Run', 'Build')) {
    & $joyDotnetCommand.Source build (Join-Path $joyRepoPath 'Joy.csproj') --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($Action -eq 'Build') { exit 0 }
}

$joyEngine = if (Test-Path -LiteralPath $joyPortableGodot) { $joyPortableGodot }
    elseif ($env:GODOT4 -and (Test-Path -LiteralPath $env:GODOT4)) { $env:GODOT4 }
    else { (Get-Command godot -ErrorAction Stop).Source }

if ($Action -eq 'Import') {
    & $joyEngine --headless --editor --path $joyRepoPath --quit
} else {
    & $joyEngine --path $joyRepoPath
}
exit $LASTEXITCODE
