param(
    [string]$GodotPath,
    [string]$OutputPath,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$joyRepo = Split-Path -Parent $PSScriptRoot
$joySearch = $joyRepo
for ($joyLevel = 0; $joyLevel -lt 5 -and $joySearch; $joyLevel++) {
    $joyDotnet = Join-Path $joySearch 'tools\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $joyDotnet) {
        $env:DOTNET_ROOT = Split-Path -Parent $joyDotnet
        $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
        $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
        if (!$GodotPath) { $GodotPath = Join-Path $joySearch 'tools\godot-4.7.2\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' }
        break
    }
    $joySearch = Split-Path -Parent $joySearch
}
if (!$GodotPath) { $GodotPath = $env:GODOT4 }
if (!(Test-Path -LiteralPath $GodotPath)) { throw 'Supply -GodotPath pointing to Godot 4.7.2 .NET.' }
if (!$OutputPath) { $OutputPath = Join-Path $joyRepo 'output\art-ui-v01\qa' }
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
$OutputPath = (Resolve-Path -LiteralPath $OutputPath).Path
$joyCaptureRoot = Join-Path $env:APPDATA 'Godot\app_userdata\Joy · 异常回收公司\captures'

Push-Location -LiteralPath $joyRepo
try {
    if (!$SkipBuild) {
        & dotnet build Joy.csproj --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
        & dotnet run --project Tests/Joy.Tests.csproj
        if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
        & $GodotPath --headless --editor --path $joyRepo --import *> (Join-Path $OutputPath 'import.log')
        if ($LASTEXITCODE -ne 0) { throw 'Resource import failed.' }
    }

    $joyCases = @('city', 'city-states', 'field', 'field-used', 'field-lit', 'field-extra', 'feedback', 'items', 'settlement', 'preview-long', 'preview-ward', 'transport', 'outcome', 'settings')
    $joyRuns = @()
    foreach ($joyCase in $joyCases) {
        foreach ($joyScale in @('1.00', '1.25', '1.50')) { $joyRuns += @{Case=$joyCase;Scale=$joyScale;Variant='a';Gray=$false;Cosmic=$false} }
    }
    foreach ($joyCase in @('title', 'item-states')) { $joyRuns += @{Case=$joyCase;Scale='1.00';Variant='a';Gray=$false;Cosmic=$false} }
    foreach ($joyCase in @('city', 'field', 'transport')) {
        foreach ($joyScale in @('1.00', '1.25', '1.50')) { $joyRuns += @{Case=$joyCase;Scale=$joyScale;Variant='b';Gray=$false;Cosmic=$false} }
        $joyRuns += @{Case=$joyCase;Scale='1.00';Variant='a';Gray=$true;Cosmic=$false}
        $joyRuns += @{Case=$joyCase;Scale='1.00';Variant='a';Gray=$false;Cosmic=$true}
    }
    $joyRecords = @()
    foreach ($joyRun in $joyRuns) {
        $joyName = "art-$($joyRun.Case)-$($joyRun.Variant)-$($joyRun.Scale)-" + $(if($joyRun.Gray){'gray'}else{'color'}) + $(if($joyRun.Cosmic){'-cosmic'}else{''})
        $joyArguments = @('--path', ('"' + $joyRepo + '"'), '--resolution', '1920x1080', '--', '--capture-art', "--case=$($joyRun.Case)", "--scale=$($joyRun.Scale)", "--variant=$($joyRun.Variant)")
        if ($joyRun.Gray) { $joyArguments += '--grayscale' }
        if ($joyRun.Cosmic) { $joyArguments += '--cosmic' }
        $joyProcess = Start-Process -FilePath $GodotPath -ArgumentList $joyArguments -WorkingDirectory $joyRepo -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $OutputPath "$joyName.log") -RedirectStandardError (Join-Path $OutputPath "$joyName.stderr.log")
        if (!$joyProcess.WaitForExit(45000)) { Stop-Process -Id $joyProcess.Id; throw "Capture timed out: $joyName" }
        if ($joyProcess.ExitCode -ne 0) { throw "Capture failed: $joyName" }
        foreach ($joyExtension in @('png', 'json')) { Copy-Item -LiteralPath (Join-Path $joyCaptureRoot "$joyName.$joyExtension") -Destination $OutputPath -Force }
        $joyWitness = Get-Content -LiteralPath (Join-Path $OutputPath "$joyName.json") -Raw -Encoding utf8 | ConvertFrom-Json
        $joyCritical = @($joyWitness.buttons | Where-Object critical)
        $joyOutside = @($joyCritical | Where-Object { !$_.insideCanvas })
        $joyErrors = Get-Content -LiteralPath (Join-Path $OutputPath "$joyName.stderr.log") -Raw
        if ($joyErrors -match 'ERROR:|Exception|SCRIPT ERROR') { throw "Runtime errors: $joyName" }
        if ($joyOutside.Count) { throw "Critical actions outside canvas: $joyName" }
        if ($joyWitness.layoutMinimum.height -gt ($joyWitness.canvas.height - 48)) { throw "Root layout exceeds canvas: $joyName" }
        $joyRecords += [ordered]@{image="$joyName.png";case=$joyRun.Case;scale=$joyRun.Scale;criticalActions=$joyCritical.Count;criticalActionsInside=$true;layoutMinimum=$joyWitness.layoutMinimum;sha256=(Get-FileHash -LiteralPath (Join-Path $OutputPath "$joyName.png")).Hash.ToLowerInvariant()}
        Write-Output "ART_QA | $joyName | critical actions in canvas"
    }
    $joyPairs = @()
    foreach ($joyCase in @('city','field','transport')) {
        foreach ($joyScale in @('1.00','1.25','1.50')) {
            $joyA = (Get-FileHash -LiteralPath (Join-Path $OutputPath "art-$joyCase-a-$joyScale-color.png")).Hash
            $joyB = (Get-FileHash -LiteralPath (Join-Path $OutputPath "art-$joyCase-b-$joyScale-color.png")).Hash
            if ($joyA -ne $joyB) { throw "Hidden A/B pixel comparison failed: $joyCase / $joyScale" }
            $joyPairs += "$joyCase / $joyScale : identical pixels"
        }
    }
    [ordered]@{capturedAtUtc=[DateTime]::UtcNow.ToString('o');canvas='1920x1080';runs=$joyRecords;hiddenInformationPairs=$joyPairs;manualReview='Inspect PNGs for wrapping, room anchors and visual state semantics; geometry checks alone do not prove acceptance.'} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputPath 'validation.json') -Encoding utf8
    Write-Output "ART_QA | $($joyRuns.Count) captures | $($joyPairs.Count) identical A/B pairs | $OutputPath"
} finally { Pop-Location }
