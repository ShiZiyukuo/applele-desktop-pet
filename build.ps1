param(
    [switch]$SkipChecks,
    [switch]$FrameworkDependent,
    [string]$OutputDirectory = 'app',
    [string]$RuntimeVersion = '8.0.30'
)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (-not $SkipChecks) {
        & dotnet restore tests/Applele.Checks --source https://api.nuget.org/v3/index.json -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { throw 'Core restore failed' }
        & dotnet run --project tests/Applele.Checks -c Release --no-restore -- --report docs/core-walk-playback.json
        if ($LASTEXITCODE -ne 0) { throw 'Core checks failed' }
    }
    $publishDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputDirectory))
    $publishArgs = @('publish','src/Applele.Windows/Applele.Windows.csproj','-c','Release','-o',$publishDirectory)
    if ($FrameworkDependent) { $publishArgs += @('--self-contained','false') }
    else { $publishArgs += @('-r','win-x64','--self-contained','true',('-p:RuntimeFrameworkVersion='+$RuntimeVersion),'--source','https://api.nuget.org/v3/index.json','-p:NuGetAudit=false') }
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
    if (-not $FrameworkDependent) {
        $cache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages' }
        $licenses = Join-Path $publishDirectory 'licenses'
        New-Item -ItemType Directory -Path $licenses -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $cache "microsoft.netcore.app.runtime.win-x64/$RuntimeVersion/LICENSE.TXT") -Destination (Join-Path $licenses 'dotnet-runtime-LICENSE.txt') -Force
        Copy-Item -LiteralPath (Join-Path $cache "microsoft.netcore.app.runtime.win-x64/$RuntimeVersion/THIRD-PARTY-NOTICES.TXT") -Destination (Join-Path $licenses 'dotnet-runtime-THIRD-PARTY-NOTICES.txt') -Force
        Copy-Item -LiteralPath (Join-Path $cache "microsoft.windowsdesktop.app.runtime.win-x64/$RuntimeVersion/LICENSE") -Destination (Join-Path $licenses 'windowsdesktop-runtime-LICENSE.txt') -Force
    }
    if (-not $SkipChecks) {
        $petProcess = Start-Process -FilePath (Join-Path $publishDirectory 'Applele.exe') -ArgumentList '--check','--report',('"' + (Join-Path $PSScriptRoot 'docs/windows-walk-playback') + '"') -WindowStyle Hidden -PassThru
        if (-not $petProcess.WaitForExit(60000)) { $petProcess.Kill(); throw 'Window checks timed out' }
        if ($petProcess.ExitCode -ne 0) { throw ('Window checks failed; see '+(Join-Path $publishDirectory 'startup-error.json')) }
        & (Join-Path $PSScriptRoot 'tests/ReleaseChecks.ps1') -PublishDirectory $publishDirectory
    }
} finally { Pop-Location }
