<#
    Publishes Glaze and wraps it for release.

    Output, in dist\:
      Glaze-Setup-<version>.exe      installer; installed copies update from this
      Glaze-<version>-portable.exe   the single exe itself; portable copies update from this
      latest.yml                     what Glaze 1.x (Electron) looks for to update, pointing at the installer

    The version comes from app\Glaze.csproj alone, so a release is bumped in exactly one place.
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'app\Glaze.csproj'
$dist = Join-Path $root 'dist'
$publishDir = Join-Path $dist 'app'

[xml]$csproj = Get-Content $project
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> in $project" }
Write-Host "Glaze $version" -ForegroundColor Cyan

# Self-contained single file: the target needs no .NET, and the portable build is this one exe.
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publishDir `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

$exe = Join-Path $publishDir 'Glaze.exe'
$portable = Join-Path $dist "Glaze-$version-portable.exe"
Copy-Item $exe $portable
Write-Host ("  portable:  {0} ({1:N1} MB)" -f $portable, ((Get-Item $portable).Length / 1MB)) -ForegroundColor Green

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "ISCC.exe not found - install Inno Setup 6" }

& $iscc "/DAppVersion=$version" (Join-Path $PSScriptRoot 'Glaze.iss') /Q
if ($LASTEXITCODE -ne 0) { throw "installer failed" }

$setup = Join-Path $dist "Glaze-Setup-$version.exe"
Write-Host ("  installer: {0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB)) -ForegroundColor Green

# Glaze 1.x updated through electron-updater, which reads latest.yml from the latest release and runs the
# file it names. Pointed at the new installer, it carries 1.x copies over to 2.x; the installer then
# removes the Electron build (installer\Glaze.iss). Only needed while 1.x copies are still around.
$bytes = [IO.File]::ReadAllBytes($setup)
$sha512 = [Convert]::ToBase64String([Security.Cryptography.SHA512]::Create().ComputeHash($bytes))
$name = Split-Path $setup -Leaf
$date = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
@"
version: $version
files:
  - url: $name
    sha512: $sha512
    size: $($bytes.Length)
path: $name
sha512: $sha512
releaseDate: '$date'
"@ | Set-Content (Join-Path $dist 'latest.yml') -Encoding ascii
Write-Host "  latest.yml for Glaze 1.x" -ForegroundColor Green
