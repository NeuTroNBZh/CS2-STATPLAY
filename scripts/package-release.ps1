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
$deploymentGuide = Join-Path $repoRoot "DEPLOYMENT_GUIDE.md"
$baselineSql = Join-Path $repoRoot "sql/001_v1_baseline_schema.sql"
$aggregationSql = Join-Path $repoRoot "sql/002_v1_aggregation_stored_procedures.sql"

if (-not $Version) {
    $projectXml = [xml](Get-Content -Path $pluginProject)
    $assemblyVersion = $projectXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($assemblyVersion)) {
        $assemblyVersion = "0.9.0"
    }

    $Version = $assemblyVersion
}

    $artifactBaseName = "$PackageId-$Version-$RuntimeIdentifier"
$artifactsRoot = Join-Path $repoRoot "artifacts"
    $publishRoot = if ([string]::IsNullOrWhiteSpace($SourceDirectory)) {
        Join-Path $artifactsRoot "publish/$artifactBaseName"
    } else {
        $SourceDirectory
    }
$packageRoot = Join-Path $artifactsRoot $artifactBaseName
$zipPath = Join-Path $artifactsRoot "$artifactBaseName.zip"

if ((-not $SkipPublish) -and (Test-Path $publishRoot)) {
    Remove-Item -Path $publishRoot -Recurse -Force
}

if (Test-Path $packageRoot) {
    Remove-Item -Path $packageRoot -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

if (-not $SkipPublish) {
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
    dotnet publish $pluginProject -c $Configuration -o $publishRoot /p:PackageOnBuild=false | Out-Host
}

if (-not (Test-Path $publishRoot)) {
    throw "Publish output not found: $publishRoot"
}

$pluginTargetDir = Join-Path $packageRoot "addons/counterstrikesharp/plugins/CS2Stats"
$configTargetDir = Join-Path $packageRoot "addons/counterstrikesharp/configs/plugins/CS2Stats"
$sqlTargetDir = Join-Path $packageRoot "sql"
$installScriptPath = Join-Path $packageRoot "install.sh"
$installPowerShellPath = Join-Path $packageRoot "install.ps1"
$changeLogPath = Join-Path $packageRoot "CHANGELOG.md"
$shaSumsPath = Join-Path $artifactsRoot "SHA256SUMS.txt"

New-Item -ItemType Directory -Path $pluginTargetDir -Force | Out-Null
New-Item -ItemType Directory -Path $configTargetDir -Force | Out-Null
New-Item -ItemType Directory -Path $sqlTargetDir -Force | Out-Null

Copy-Item -Path (Join-Path $publishRoot "*") -Destination $pluginTargetDir -Recurse -Force
Copy-Item -Path $exampleConfig -Destination (Join-Path $configTargetDir "CS2Stats.json") -Force
Copy-Item -Path $deploymentGuide -Destination (Join-Path $packageRoot "DEPLOYMENT_GUIDE.md") -Force
Copy-Item -Path $baselineSql -Destination (Join-Path $sqlTargetDir "001_v1_baseline_schema.sql") -Force
Copy-Item -Path $aggregationSql -Destination (Join-Path $sqlTargetDir "002_v1_aggregation_stored_procedures.sql") -Force

$installScriptContent = @(
    '#!/usr/bin/env bash',
    'set -euo pipefail',
    '',
    'if [ $# -ne 1 ]; then',
    "  echo 'Usage: ./install.sh /path/to/cs2-server-root'",
    '  exit 1',
    'fi',
    '',
    'TARGET_ROOT="$1"',
    'SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" ; pwd)"',
    '',
    'mkdir -p "$TARGET_ROOT"',
    'cp -R "$SCRIPT_DIR/addons" "$TARGET_ROOT/"',
    '',
    "echo 'Plugin files copied successfully.'",
    "echo 'Next steps:'",
    "echo '1. Edit addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json'",
    "echo '2. Import sql/001_v1_baseline_schema.sql and sql/002_v1_aggregation_stored_procedures.sql into MySQL'",
    "echo '3. Restart the server or reload CounterStrikeSharp plugins'"
)
Set-Content -Path $installScriptPath -Value $installScriptContent -Encoding ascii

$installPowerShellContent = @(
    'param(',
    '    [Parameter(Mandatory = $true)]',
    '    [string]$ServerRoot',
    ')',
    '',
    '$ErrorActionPreference = "Stop"',
    '$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path',
    '$sourceAddons = Join-Path $packageRoot "addons"',
    '',
    'if (-not (Test-Path $ServerRoot)) {',
    '    New-Item -ItemType Directory -Path $ServerRoot -Force | Out-Null',
    '}',
    '',
    'Copy-Item -Path $sourceAddons -Destination $ServerRoot -Recurse -Force',
    'Write-Host "Plugin files copied successfully."',
    'Write-Host "Next steps:"',
    'Write-Host "1. Edit addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json"',
    'Write-Host "2. Import sql/001_v1_baseline_schema.sql and sql/002_v1_aggregation_stored_procedures.sql into MySQL"',
    'Write-Host "3. Restart the server or reload CounterStrikeSharp plugins"'
)
Set-Content -Path $installPowerShellPath -Value $installPowerShellContent -Encoding ascii

$changeLogContent = @(
    "# Changelog",
    "",
    "## $Version",
    "",
    "- Release package standardized as $PackageId-$Version-$RuntimeIdentifier",
    "- CounterStrikeSharp-ready folder and zip artifacts generated automatically",
    "- Included SQL schema and aggregation procedures in the package",
    "- Added Linux and Windows install scripts",
    "- Added GitHub Actions workflows for CI artifacts and tagged releases"
)
Set-Content -Path $changeLogPath -Value $changeLogContent -Encoding ascii

$readmePath = Join-Path $packageRoot "README_PACKAGE.txt"
$readmeContent = @(
    "$PackageId package $Version",
    "",
    "This archive is ready to copy into a Counter-Strike 2 server root.",
    "",
    "Contents:",
    "- addons/counterstrikesharp/plugins/CS2Stats",
    "- addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json",
    "- sql/001_v1_baseline_schema.sql",
    "- sql/002_v1_aggregation_stored_procedures.sql",
    "- install.sh",
    "- install.ps1",
    "- CHANGELOG.md",
    "- DEPLOYMENT_GUIDE.md",
    "",
    "Quick install:",
    "1. Extract the archive at the root of the server.",
    "2. Or run ./install.sh /path/to/cs2-server-root on Linux.",
    "3. Or run ./install.ps1 -ServerRoot C:\\path\\to\\cs2-server on Windows PowerShell.",
    "4. Edit addons/counterstrikesharp/configs/plugins/CS2Stats/CS2Stats.json.",
    "5. Import sql/001_v1_baseline_schema.sql and sql/002_v1_aggregation_stored_procedures.sql into MySQL.",
    "6. Restart the server or reload CounterStrikeSharp plugins."
)
Set-Content -Path $readmePath -Value $readmeContent -Encoding ascii

Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

$zipHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$shaContent = @(
    "$zipHash *$artifactBaseName.zip"
)
Set-Content -Path $shaSumsPath -Value $shaContent -Encoding ascii

Write-Host "Package folder created: $packageRoot"
Write-Host "Package zip created: $zipPath"
Write-Host "Checksums file created: $shaSumsPath"