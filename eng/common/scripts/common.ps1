$RepoRoot = Resolve-Path "${PSScriptRoot}..\..\..\.."
$EngDir = Join-Path $RepoRoot "eng"
$EngCommonDir = Join-Path $EngDir "common"
$EngCommonScriptsDir = Join-Path $EngCommonDir "scripts"
$EngScriptsDir = Join-Path $EngDir "scripts"

# Import required scripts
. (Join-Path $EngCommonScriptsDir SemVer.ps1)
. (Join-Path $EngCommonScriptsDir ChangeLog-Operations.ps1)
. (Join-Path $EngCommonScriptsDir Package-Properties.ps1)

# Setting expected from common languages settings
$Language = "Unknown"
$PackageRepository = "Unknown"
$packagePattern = "Unknown"
$MetadataUri = "Unknown"

# Import common language settings
$EngScriptsLanguageSettings = Join-path $EngScriptsDir "Language-Settings.ps1"
if (Test-Path $EngScriptsLanguageSettings) {
  . $EngScriptsLanguageSettings
}
if ($null -eq $LanguageShort)
{
  $LangaugeShort = $Language
}
if ($null -eq $isDevOpsRun)
{
  $isDevOpsRun = ($null -ne $env:SYSTEM_TEAMPROJECTID)
}

# Transformed Functions
$GetPackageInfoFromRepoFn = "Get-${Language}-PackageInfoFromRepo"
$GetPackageInfoFromPackageFileFn = "Get-${Language}-PackageInfoFromPackageFile"
$PublishGithubIODocsFn = "Publish-${Language}-GithubIODocs"

function LogHelper ($logType, $logArgs)
{
  if ($isDevOpsRun) 
  {
    Write-Host "##vso[task.LogIssue type=$logType;]$logArgs"
  }
  else 
  {
    Write-Warning "$logArgs"
  }
}

function LogWarning
{
  LogHelper -logType "warning" -logArgs $args
}

function LogError
{
  LogHelper -logType "error" -logArgs $args
}

function LogDebug
{
  LogHelper -logType "debug" -logArgs $args
}