param(
    [string]$Configuration = "Release",
    [string]$Version,
    [string]$RuntimeIdentifier = "linux-x64",
    [string]$PackageId = "CS2-STATPLAY",
    [string]$SourceDirectory,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

$pluginProject = Join-Path $repoRoot "src/CS2Stats.Plugin/CS2Stats.Plugin.csproj"
$exampleConfig = Join-Path $repoRoot "config/cs2stats.example.json"
$pluginAssemblyName = "CS2Stats"

if (-not $Version) {
    $projectXml = [xml](Get-Content -Path $pluginProject)
    $assemblyVersion = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($assemblyVersion)) {
        $assemblyVersion = "1.0.0"
    }

    $Version = $assemblyVersion
}

$artifactBaseName = "$PackageId-$Version-$RuntimeIdentifier"
$updateArtifactBaseName = "$artifactBaseName-update-no-config"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$buildTimestamp = Get-Date
$publishRoot = if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
    Join-Path $artifactsRoot "publish/$artifactBaseName"
} else {
    $SourceDirectory
}
$packageRoot = Join-Path $artifactsRoot $artifactBaseName
$updatePackageRoot = Join-Path $artifactsRoot $updateArtifactBaseName
$zipPath = Join-Path $artifactsRoot "$artifactBaseName.zip"
$updateZipPath = Join-Path $artifactsRoot "$updateArtifactBaseName.zip"

if ((-not $SkipPublish) -and (Test-Path $publishRoot)) {
    Remove-Item -Path $publishRoot -Recurse -Force
}

if (Test-Path $packageRoot) {
    Remove-Item -Path $packageRoot -Recurse -Force
}

if (Test-Path $updatePackageRoot) {
    Remove-Item -Path $updatePackageRoot -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

if (Test-Path $updateZipPath) {
    Remove-Item -Path $updateZipPath -Force
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
New-Item -ItemType Directory -Path $updatePackageRoot -Force | Out-Null

if (-not $SkipPublish) {
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
    dotnet publish $pluginProject -c $Configuration -o $publishRoot /p:PackageOnBuild=false | Out-Host
}

if (-not (Test-Path $publishRoot)) {
    throw "Publish output not found: $publishRoot"
}

$shaSumsPath = Join-Path $artifactsRoot "SHA256SUMS.txt"

function New-PackageVariant {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VariantRoot,
        [Parameter(Mandatory = $true)]
        [bool]$IncludeConfig
    )

    $pluginTargetDir = Join-Path $VariantRoot "addons/counterstrikesharp/plugins/$pluginAssemblyName"
    New-Item -ItemType Directory -Path $pluginTargetDir -Force | Out-Null
    Copy-Item -Path (Join-Path $publishRoot "*") -Destination $pluginTargetDir -Recurse -Force

    if ($IncludeConfig) {
        $configTargetDir = Join-Path $VariantRoot "addons/counterstrikesharp/configs/plugins/$pluginAssemblyName"
        New-Item -ItemType Directory -Path $configTargetDir -Force | Out-Null
        Copy-Item -Path $exampleConfig -Destination (Join-Path $configTargetDir "$pluginAssemblyName.json") -Force
    }

    Get-ChildItem -Path $VariantRoot -Recurse -Force | ForEach-Object {
        $_.LastWriteTime = $buildTimestamp
    }
}

New-PackageVariant -VariantRoot $packageRoot -IncludeConfig $true
New-PackageVariant -VariantRoot $updatePackageRoot -IncludeConfig $false

Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $updatePackageRoot "*") -DestinationPath $updateZipPath -CompressionLevel Optimal

$shaContent = @()

foreach ($currentZip in @($zipPath, $updateZipPath)) {
    $zipHash = (Get-FileHash -Path $currentZip -Algorithm SHA256).Hash.ToLowerInvariant()
    $shaContent += "$zipHash *$(Split-Path -Leaf $currentZip)"
}

Set-Content -Path $shaSumsPath -Value $shaContent -Encoding ascii

Write-Host "Package folder (with config) created: $packageRoot"
Write-Host "Package zip (with config) created: $zipPath"
Write-Host "Package folder (update no config) created: $updatePackageRoot"
Write-Host "Package zip (update no config) created: $updateZipPath"
Write-Host "Checksums file created: $shaSumsPath"