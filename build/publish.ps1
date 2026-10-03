<#
.SYNOPSIS
    Builds the Pinny Notes release artifacts into .\dist

.DESCRIPTION
    For each runtime this produces:
      PinnyNotes-<version>-<arch>.exe             Self-contained single-file exe, no .NET install needed.
      PinnyNotes-Portable-<version>-<arch>.zip    Same exe plus portable.txt, notes are stored next to the exe.
      PinnyNotes-Setup-<version>-<arch>.msi       Self-contained installer (WiX).
    plus SHA256SUMS.txt covering all of the above.

.EXAMPLE
    .\build\publish.ps1
    .\build\publish.ps1 -Version 1.18.0 -Runtimes win-x64
#>
param(
    [string]$Version,
    [string[]]$Runtimes = @('win-x64', 'win-arm64'),
    [string]$Output = 'dist',
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'PinnyNotes.WpfUi\PinnyNotes.WpfUi.csproj'
$installerProject = Join-Path $root 'PinnyNotes.Installer\PinnyNotes.Installer.wixproj'

if (-not $Version) {
    $Version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
Write-Host "Building Pinny Notes $Version for $($Runtimes -join ', ')" -ForegroundColor Cyan

if (-not [System.IO.Path]::IsPathRooted($Output)) { $Output = Join-Path $root $Output }
$work = Join-Path $Output '_work'
if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
New-Item -ItemType Directory -Force $work | Out-Null

# MSBuild properties must be passed quoted, pwsh 7 otherwise splits -p:Name=Value into two arguments
function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE" }
}

$common = @('-c', 'Release', '--self-contained', 'true', '-p:DebugType=none', "-p:Version=$Version", '-nologo')

foreach ($rid in $Runtimes) {
    $arch = $rid.Split('-')[1]

    # Single-file exe
    $singleDir = Join-Path $work "single-$arch"
    Invoke-DotNet publish $project -r $rid @common -o $singleDir `
        '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:EnableCompressionInSingleFile=true'
    $exe = Join-Path $singleDir 'Pinny Notes.exe'
    Copy-Item $exe (Join-Path $Output "PinnyNotes-$Version-$arch.exe")

    # Portable zip, the portable.txt marker keeps the database next to the exe
    $portableRoot = Join-Path $work "portable-$arch"
    $portableDir = Join-Path $portableRoot 'Pinny Notes'
    New-Item -ItemType Directory -Force $portableDir | Out-Null
    Copy-Item $exe $portableDir
    New-Item -ItemType File (Join-Path $portableDir 'portable.txt') | Out-Null
    # Windows' bsdtar writes standard zips, Compress-Archive on PowerShell 5.1 uses backslashes in entry paths
    & "$env:SystemRoot\System32\tar.exe" -a -c -f (Join-Path $Output "PinnyNotes-Portable-$Version-$arch.zip") -C $portableRoot 'Pinny Notes'
    if ($LASTEXITCODE -ne 0) { throw "Creating portable zip failed with exit code $LASTEXITCODE" }

    # MSI installer from a regular (multi-file) self-contained publish
    if (-not $SkipInstaller) {
        $folderDir = Join-Path $work "folder-$arch"
        Invoke-DotNet publish $project -r $rid @common -o $folderDir
        $platform = if ($arch -eq 'arm64') { 'ARM64' } else { 'x64' }
        Invoke-DotNet build $installerProject -c Release -nologo `
            "-p:Platform=$platform" "-p:Version=$Version" "-p:PublishDir=$folderDir\" `
            "-p:OutputName=PinnyNotes-Setup-$Version-$arch" "-p:OutputPath=$Output\" `
            "-p:IntermediateOutputPath=$work\wixobj-$arch\"
    }
}

Remove-Item $work -Recurse -Force
Get-ChildItem $Output -File | Where-Object { $_.Extension -in '.wixpdb', '.pdb' } | Remove-Item

$sums = Get-ChildItem $Output -File | Sort-Object Name | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)"
}
$sums | Set-Content (Join-Path $Output 'SHA256SUMS.txt') -Encoding ascii

Write-Host "`nArtifacts in ${Output}:" -ForegroundColor Green
Get-ChildItem $Output -File | ForEach-Object { '{0,-45} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
