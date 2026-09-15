param(
    [string]$OutputRoot,
    [string]$GodotPath
)

$ErrorActionPreference = 'Stop'
$joyRepo = Split-Path -Parent $PSScriptRoot
$joyWorkspace = Split-Path -Parent $joyRepo
$joySdk = Join-Path $joyWorkspace 'tools\dotnet'
$joyPortableEngine = Join-Path $joyWorkspace 'tools\godot-4.7.2\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
if (Test-Path -LiteralPath (Join-Path $joySdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $joySdk
    $env:DOTNET_ROOT_X64 = $joySdk
    $env:PATH = "$joySdk;$env:PATH"
}
$joyDotnet = (Get-Command dotnet -ErrorAction Stop).Source
if (!$GodotPath) {
    $GodotPath = if (Test-Path -LiteralPath $joyPortableEngine) { $joyPortableEngine }
        elseif ($env:GODOT4) { $env:GODOT4 }
        else { (Get-Command godot -ErrorAction Stop).Source }
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
if (!$OutputRoot) { $OutputRoot = Join-Path $joyWorkspace 'builds' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if ($OutputRoot.TrimEnd('\', '/') -eq $joyRepo -or $OutputRoot.StartsWith($joyRepo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose an output directory outside the Godot project to prevent exported files being re-imported.'
}

Push-Location -LiteralPath $joyRepo
try {
    [xml]$joyProject = Get-Content -LiteralPath (Join-Path $joyRepo 'Joy.csproj') -Raw
    $joyVersion = [string]$joyProject.Project.PropertyGroup.Version
    if ($joyVersion -notmatch '^\d+\.\d+\.\d+(?:-[A-Za-z0-9.]+)?$') { throw 'Invalid project version.' }
    $joyCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the source commit.' }
    & git diff --quiet HEAD -- Core Scripts Scenes Assets Licenses tools/release.ps1 Joy.csproj project.godot export_presets.cfg
    if ($LASTEXITCODE -ne 0) { throw 'Commit release source and configuration before packaging.' }

    $joyName = "Joy-v$joyVersion-windows-x64"
    $joyRun = Join-Path $OutputRoot ("release-v$joyVersion-" + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
    $joyPackage = Join-Path $joyRun $joyName
    New-Item -ItemType Directory -Path $joyPackage -Force | Out-Null

    & $joyDotnet run --project (Join-Path $joyRepo 'Tests\Joy.Tests.csproj')
    if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }

    function Invoke-JoyExportStep([string]$Stage, [string[]]$EngineArguments) {
        $joyOut = Join-Path $joyRun "$Stage.stdout.log"
        $joyErr = Join-Path $joyRun "$Stage.stderr.log"
        $joyProcess = Start-Process -FilePath $GodotPath -ArgumentList $EngineArguments -WorkingDirectory $joyRepo -WindowStyle Hidden -Wait -PassThru -RedirectStandardOutput $joyOut -RedirectStandardError $joyErr
        Get-Content -LiteralPath $joyOut | Out-Host
        Get-Content -LiteralPath $joyErr | Out-Host
        if ($joyProcess.ExitCode -ne 0) { throw "Godot $Stage failed: $($joyProcess.ExitCode). See $joyRun" }
    }

    Invoke-JoyExportStep -Stage 'version' -EngineArguments @('--headless', '--version')
    $joyEngineVersion = (Get-Content -LiteralPath (Join-Path $joyRun 'version.stdout.log') -Raw).Trim()
    if ($joyEngineVersion -notmatch '^4\.7\.2\.stable\.mono(?:\.|$)') { throw "Expected Godot 4.7.2 .NET, got: $joyEngineVersion" }
    Invoke-JoyExportStep -Stage 'import' -EngineArguments @('--headless', '--editor', '--path', ('"' + $joyRepo + '"'), '--import')
    Invoke-JoyExportStep -Stage 'export' -EngineArguments @('--headless', '--path', ('"' + $joyRepo + '"'), '--export-release', '"Windows Desktop"', ('"' + (Join-Path $joyPackage 'Joy.exe') + '"'))

    $joyData = Join-Path $joyPackage 'data_Joy_windows_x86_64'
    foreach ($joyRequired in @('Joy.exe', 'Joy.pck', 'data_Joy_windows_x86_64\Joy.dll', 'data_Joy_windows_x86_64\hostfxr.dll', 'data_Joy_windows_x86_64\hostpolicy.dll', 'data_Joy_windows_x86_64\coreclr.dll')) {
        if (!(Test-Path -LiteralPath (Join-Path $joyPackage $joyRequired) -PathType Leaf)) { throw "Missing export file: $joyRequired" }
    }
    $joyRuntime = Get-Content -LiteralPath (Join-Path $joyData 'Joy.runtimeconfig.json') -Raw | ConvertFrom-Json
    $joyRuntimeVersion = @($joyRuntime.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App')[0].version
    if (!$joyRuntimeVersion) { throw 'The export must include its own .NET runtime.' }
    $joyNuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    $joyRuntimePackage = Join-Path $joyNuget "microsoft.netcore.app.runtime.win-x64\$joyRuntimeVersion"
    $joyLicenses = Join-Path $joyPackage 'Licenses'
    Copy-Item -LiteralPath (Join-Path $joyRepo 'Licenses') -Destination $joyLicenses -Recurse
    Copy-Item -LiteralPath (Join-Path $joyRuntimePackage 'LICENSE.TXT') -Destination (Join-Path $joyLicenses 'DotNet-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $joyRuntimePackage 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $joyLicenses 'DotNet-THIRD-PARTY-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $joyRepo "docs\releases\v$joyVersion.md") -Destination (Join-Path $joyPackage 'PLAYTEST.md')

    @"
Joy v$joyVersion - Windows 64 位开发者试玩版

完整解压本 ZIP 到可写目录，然后双击 Joy.exe。
请保留 Joy.pck、data_Joy_windows_x86_64 和其它随附文件。
已包含 .NET 运行时；无需安装 Godot 编辑器、.NET SDK 或另行下载运行时。

这是 M2 单城原型预发行版本。试玩内容、操作提示与反馈建议见 PLAYTEST.md，游戏内也有操作手册。
存档自动保存在 %APPDATA%\Godot\app_userdata\Joy · 异常回收公司\profile-m2。
反馈时请附经营日、种子、操作顺序、截图及 build-info.json 中的 sourceCommit。

包内中文字体采用 Noto Sans CJK。Godot、.NET 与字体许可见 Licenses 目录。
项目与问题反馈：https://github.com/bddfs/Joy
"@ | Set-Content -LiteralPath (Join-Path $joyPackage 'README.txt') -Encoding utf8

    [ordered]@{
        version = $joyVersion
        tag = "v$joyVersion"
        sourceCommit = $joyCommit
        builtAtUtc = [DateTime]::UtcNow.ToString('o')
        platform = 'windows-x64'
        godot = $joyEngineVersion
        dotnetRuntime = $joyRuntimeVersion
        selfContained = $true
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $joyPackage 'build-info.json') -Encoding utf8

    $joyZip = Join-Path $joyRun "$joyName.zip"
    Compress-Archive -LiteralPath $joyPackage -DestinationPath $joyZip -CompressionLevel Optimal
    $joyHash = (Get-FileHash -LiteralPath $joyZip -Algorithm SHA256).Hash.ToLowerInvariant()
    "$joyHash  $joyName.zip" | Set-Content -LiteralPath (Join-Path $joyRun 'SHA256SUMS.txt') -Encoding ascii
    [ordered]@{
        zip = $joyZip
        sha256File = (Join-Path $joyRun 'SHA256SUMS.txt')
        sha256 = $joyHash
        bytes = (Get-Item -LiteralPath $joyZip).Length
        sourceCommit = $joyCommit
    } | ConvertTo-Json | Tee-Object -FilePath (Join-Path $joyRun 'release-result.json')
} finally {
    Pop-Location
}
