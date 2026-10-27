param(
    [string]$OutputRoot,
    [string]$GodotPath
)

$ErrorActionPreference = 'Stop'
$joyRepo = Split-Path -Parent $PSScriptRoot
$joyWorkspace = Split-Path -Parent $joyRepo
$joySdk = $null
$joyPortableEngine = $null
$joySearch = $joyRepo
# Check the repository and at most four parents, including managed worktree layouts.
for ($joyLevel = 0; $joyLevel -lt 5 -and $joySearch; $joyLevel++) {
    $joySdkCandidate = Join-Path $joySearch 'tools\dotnet'
    $joyEngineCandidate = Join-Path $joySearch 'tools\godot-4.7.2\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
    if (!$joySdk -and (Test-Path -LiteralPath (Join-Path $joySdkCandidate 'dotnet.exe') -PathType Leaf)) {
        $joySdk = $joySdkCandidate
        $joyWorkspace = $joySearch
    }
    if (!$joyPortableEngine -and (Test-Path -LiteralPath $joyEngineCandidate -PathType Leaf)) { $joyPortableEngine = $joyEngineCandidate }
    if ($joySdk -and $joyPortableEngine) { break }
    $joyParent = Split-Path -Parent $joySearch
    if ($joyParent -eq $joySearch) { break }
    $joySearch = $joyParent
}
if ($joySdk) {
    $env:DOTNET_ROOT = $joySdk
    $env:DOTNET_ROOT_X64 = $joySdk
    $env:PATH = "$joySdk;$env:PATH"
}
$joyDotnet = (Get-Command dotnet -ErrorAction Stop).Source
if (!$GodotPath) {
    $GodotPath = if ($joyPortableEngine) { $joyPortableEngine }
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
    $joyDirty = @(& git status --porcelain --untracked-files=all -- Core Scripts Scenes Assets Licenses docs/art "docs/releases/v$joyVersion.md" README.md tools/release.ps1 Joy.csproj project.godot export_presets.cfg)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect release source status.' }
    if ($joyDirty.Count) { throw 'Commit release source, asset notices and configuration before packaging.' }

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

    # Explicit metadata allowlist: do not copy source trees, generation caches or QA output.
    $joyArtDirectory = Join-Path $joyPackage 'Art'
    New-Item -ItemType Directory -Path $joyArtDirectory -Force | Out-Null
    $joyManifestPath = Join-Path $joyRepo 'docs\art\assets.json'
    $joyArtManifest = Get-Content -LiteralPath $joyManifestPath -Raw -Encoding utf8 | ConvertFrom-Json
    Copy-Item -LiteralPath $joyManifestPath -Destination (Join-Path $joyArtDirectory 'assets.json')
    @"
Joy AI 美术素材说明 / $($joyArtManifest.artVersion)

候选状态：$($joyArtManifest.status)
生成来源：$($joyArtManifest.rights.creation)
参考来源：$($joyArtManifest.rights.reference)
权利边界：$($joyArtManifest.rights.license)
团队审美批准状态：$($joyArtManifest.rights.teamApproval)

完整资产 ID、来源、生成记录及文件校验见 Art/assets.json。
游戏素材由 Joy.pck 提供；清单中的源码和提示词路径对应源码仓库，不表示包内存在另一份实物或素材。
本包仅附资产清单与本说明，不附开发缓存、生成缓存或整套开发工具。
"@ | Set-Content -LiteralPath (Join-Path $joyLicenses 'AI-Art-NOTICE.txt') -Encoding utf8

    @"
Joy v$joyVersion - Windows 64 位开发者试玩版

完整解压本 ZIP 到可写目录，然后双击 Joy.exe。
请保留 Joy.pck、data_Joy_windows_x86_64 和其它随附文件。
已包含 .NET 运行时；无需安装 Godot 编辑器、.NET SDK 或另行下载运行时。

这是网格地图、角色构筑和全卡牌操作的独立玩法原型，版本 $joyVersion。
试玩内容、操作提示与反馈建议见 PLAYTEST.md，游戏内也有操作手册。
存档自动保存在 %APPDATA%\Godot\app_userdata\Joy · 异常回收公司\grid-prototype-v2。
首次继续可导入旧网格版进度，将专精映射为天赋节点，原v1文件保留。旧M2试玩进度独立。
反馈时请附经营日、种子、操作顺序、截图及 build-info.json 中的 sourceCommit。

包内中文字体采用 Noto Sans CJK。Godot、.NET 与字体许可见 Licenses 目录。
资产清单见 Art/assets.json，AI 素材来源及权利说明见 Licenses/AI-Art-NOTICE.txt。
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
        artVersion = $joyArtManifest.artVersion
        assetManifest = 'Art/assets.json'
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
