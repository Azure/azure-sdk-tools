<#
.SYNOPSIS
Create a default assets.json for a given ServiceDirectory or deeper.

.DESCRIPTION
Requirements:
1. git will need to be in the path.
2. This script will need to be run locally in a an azure-sdk-for-<language> repository. Further, this
needs to be run at an sdk/<ServiceDirectory> or deeper. For example sdk/core if the assets.json is
being created at the service directory level or sdk/core/<somelibrary> if the assets.json is being
created at the library level. A good rule here would be to run this in the same directory where the ci.yml
file lives. For most this is the sdk/<ServiceDirectory> but, in some service directories, each library
has its own ci.yml and pipeline and for these the ci.yml would be in the sdk/<ServiceDirectory>/<library>.

Generated assets.json file contents
1. AssetsRepo: "Azure/azure-sdk-assets" - This is the assets repository
2. AssetsRepoPrefixPath: "<language>" - this is will be computed from repository it's being run in
3. TagPrefix: "<language>/<ServiceDirectory>" or "<language>/<ServiceDirectory>/<library>" or deeper if things
              are nested in such a manner.
4. Tag: "" - Initially empty, as nothing has yet been pushed
#>
param(
  [switch] $InitialPush,
  [switch] $UseTestRepo
)

# Git needs to be in the path to determine the language and, if the initial push
# is being performed, for the CLI commands to work
$GitExe = "git"
# If the initial push is being performed, test-proxy needs to be in the path in
# order for the CLI commands to be executed
$TestProxyExe = "test-proxy"

$OriginalProxyAssetsFolder = $env:PROXY_ASSETS_FOLDER

# The built test proxy on a dev machine will have the version 1.0.0-dev.20221013.1
# whereas the one installed from nuget will have the version 20221013.1 (minus the 1.0.0-dev.)
$MinTestProxyVersion = "20221012.1"

$DefaultAssetsRepo = "Azure/azure-sdk-assets"
if ($UseTestRepo) {
  $DefaultAssetsRepo = "Azure/azure-sdk-assets-integration"
  Write-Host "UseTestRepo was true, setting default repo to $DefaultAssetsRepo"
}

# Unsure of the following language recording directories:
# 1. andriod
# 2. c
# 3. ios
$LangRecordingDirs = @{"cpp" = "recordings";
  "go"                       = "recordings";
  "java"                     = "session-records";
  "js"                       = "recordings";
  "net"                      = "SessionRecords";
  "python"                   = "recordings";
};

class Assets {
  [string]$AssetsRepo = $DefaultAssetsRepo
  [string]$AssetsRepoPrefixPath = ""
  [string]$TagPrefix = ""
  [string]$Tag = ""
  Assets(
    [string]$AssetsRepoPrefixPath,
    [string]$TagPrefix
  ) {
    $this.TagPrefix = $TagPrefix
    $this.AssetsRepoPrefixPath = $AssetsRepoPrefixPath
  }
}

class Version {
  [int]$Year
  [int]$Month
  [int]$Day
  [int]$Revision
  Version(
    [string]$VersionString
  ) {
    if ($VersionString -match "(?<year>20\d{2})(?<month>\d{2})(?<day>\d{2}).(?<revision>\d+)") {
      $this.Year = [int]$Matches["year"]
      $this.Month = [int]$Matches["month"]
      $this.Day = [int]$Matches["day"]
      $this.Revision = [int]$Matches["revision"]
    }
    else {
      # This should be a Write-Error however powershell apparently cannot utilize that
      # in the constructor in certain cases
      Write-Warning "Version String '$($VersionString)' is invalid and cannot be parsed"
      exit 1
    }
  }
  [bool] IsGreaterEqual([string]$OtherVersionString) {
    [Version]$OtherVersion = [Version]::new($OtherVersionString)
    if ($this.Year -lt $OtherVersion.Year) {
      return $false
    }
    elseif ($this.Year -eq $OtherVersion.Year) {
      if ($this.Month -lt $OtherVersion.Month) {
        return $false
      }
      elseif ($this.Month -eq $OtherVersion.Month) {
        if ($this.Day -lt $OtherVersion.Day) {
          return $false
        }
        elseif ($this.Day -eq $OtherVersion.Day) {
          if ($this.Revision -lt $OtherVersion.Revision) {
            return $false
          }
        }
      }
    }
    return $true
  }
}

Function Test-Exe-In-Path {
  Param([string] $ExeToLookFor)
  if ($null -eq (Get-Command $ExeToLookFor -ErrorAction SilentlyContinue)) {
    Write-Error "Unable to find $ExeToLookFor in your PATH"
    exit 1
  }
}

Function Test-TestProxyVersion {
  param(
    [string] $TestProxyExe
  )

  Write-Host "$TestProxyExe --version"
  [string] $output = & "$TestProxyExe" --version

  [Version]$CurrentProxyVersion = [Version]::new($output)
  if (!$CurrentProxyVersion.IsGreaterEqual($MinTestProxyVersion)) {
    Write-Error "$TestProxyExe version, $output, is less than the minimum version $MinTestProxyVersion"
    Write-Error "Please refer to https://github.com/Azure/azure-sdk-tools/blob/main/tools/test-proxy/Azure.Sdk.Tools.TestProxy/README.md#installation to upgrade your $TestProxyExe"
    exit 1
  }
}

Function Get-Repo-Language {

  $GitRepoOnDiskErr = "This script can only be called from within an azure-sdk-for-<lang> repository on disk."
  # Git remote -v is going to give us output similar to the following
  # origin  git@github.com:Azure/azure-sdk-for-java.git (fetch)
  # origin  git@github.com:Azure/azure-sdk-for-java.git (push)
  # upstream        git@github.com:Azure/azure-sdk-for-java (fetch)
  # upstream        git@github.com:Azure/azure-sdk-for-java (push)
  # We're really only trying to get the language from the git remote
  Write-Host "git remote -v"
  [array] $remotes = & git remote -v
  foreach ($line in $remotes) {
    Write-Host "$line"
  }

  # Git remote -v returned "fatal: not a git repository (or any of the parent directories): .git"
  # and the list of remotes will be null
  if (-not $remotes) {
    Write-Error $GitRepoOnDiskErr
    exit 1
  }

  # The regular expression needed to be updated to handle the following types of input:
  # origin git@github.com:Azure/azure-sdk-for-python.git (fetch)
  # origin git@github.com:Azure/azure-sdk-for-python-pr.git (fetch)
  # fork git@github.com:UserName/azure-sdk-for-python (fetch)
  # azure-sdk https://github.com/azure-sdk/azure-sdk-for-net.git (fetch)
  # ForEach-Object splits the string on whitespace so each of the above strings is actually
  # 3 different strings. The first and last pieces won't match anything, the middle string
  # will match what is below. If the regular expression needs to be updated the following
  # link below will go to a regex playground
  # https://regex101.com/r/btVW5A/1
  $lang = $remotes[0] | ForEach-Object { if ($_ -match "azure-sdk-for-(?<lang>[^\-\.]+)") {
      #Return the named language match
      return $Matches["lang"]
    }
  }

  if ([String]::IsNullOrWhitespace($lang)) {
    Write-Error $GitRepoOnDiskErr
    exit 1
  }

  Write-Host "Current language=$lang"
  return $lang
}

Function Get-Repo-Root {
  [string] $currentDir = Get-Location
  # -1 to strip off the trialing directory separator
  return $currentDir.Substring(0, $currentDir.LastIndexOf("sdk") - 1)
}

Function New-Assets-Json-File {
  param(
    [Parameter(Mandatory = $true)]
    [string] $Language
  )
  $AssetsRepoPrefixPath = $Language

  [string] $currentDir = Get-Location

  $sdkDir = "$([IO.Path]::DirectorySeparatorChar)sdk$([IO.Path]::DirectorySeparatorChar)"

  # if we're not in a <reporoot>/sdk/<ServiceDirectory> or deeper then this script isn't
  # being run in the right place
  if (-not $currentDir.contains($sdkDir)) {
    Write-Error "This script needs to be run at an sdk/<ServiceDirectory> or deeper."
    exit 1
  }

  $TagPrefix = $currentDir.Substring($currentDir.LastIndexOf("sdk") + 4)
  $TagPrefix = $TagPrefix.Replace("\", "/")
  $TagPrefix = "$($AssetsRepoPrefixPath)/$($TagPrefix)"
  [Assets]$Assets = [Assets]::new($AssetsRepoPrefixPath, $TagPrefix)

  $AssetsJson = $Assets | ConvertTo-Json

  $AssetsFileName = Join-Path -Path $currentDir -ChildPath "assets.json"
  Write-Host "Writing file $AssetsFileName with the following contents"
  Write-Host $AssetsJson
  $Assets | ConvertTo-Json | Out-File $AssetsFileName

  return $AssetsFileName
}

# Invoke the proxy command and echo the output.
Function Invoke-ProxyCommand {
  param(
    [string] $TestProxyExe,
    [string] $CommandArgs
  )

  Write-Host "$TestProxyExe $CommandArgs"
  [array] $output = & "$TestProxyExe" $CommandArgs.Split(" ")
  # echo the command output
  foreach ($line in $output) {
    Write-Host "$line"
  }
}

Function Get-TempPath {
  return [System.IO.Path]::GetTempPath()
}
# Set the PROXY_ASSETS_FOLDER to [System.IO.Path]::GetTempPath()/<Guid>
# This is a temporary directory that'll be used for the restore/push operatios
# on the assets.json that was just created. This is temporary, as the original
# PROXY_ASSETS_FOLDER value was saved at the beginning of the script and
# the original value will be restored at the end of the script.
Function Set-ProxyAssetsFolder {
  $guid = [Guid]::NewGuid()
  $tempPath = Get-TempPath
  $proxyAssetsFolder = Join-Path -Path $tempPath -ChildPath $guid
  New-Item -Type Directory -Force -Path $proxyAssetsFolder | Out-Null
  $env:PROXY_ASSETS_FOLDER = $proxyAssetsFolder
  return $proxyAssetsFolder
}

# Get the shorthash directory under PROXY_ASSETS_FOLDER
Function Get-AssetsRoot {
  param(
    [string] $AssetsJsonFile
  )

  $startingPath = $env:PROXY_ASSETS_FOLDER
  # It's odd that $folder.Count and $folders.Lenght work and we need to do this
  $numDirs = Get-ChildItem $startingPath -Directory | Measure-Object | ForEach-Object { $_.Count }
  $folders = Get-ChildItem $startingPath -Directory
  # There should only be one folder
  if (1 -ne $numDirs) {
    Write-Error "The assets directory ($startingPath) should only contain 1 subfolder not $numDirs ($folders -join $([Environment]::NewLine))"
    exit 1
  }
  $assetsRoot = $folders[0].FullName
  $repoRoot = Get-Repo-Root
  $assets = Get-Content $AssetsJsonFile | Out-String | ConvertFrom-Json
  $assetsJsonPath = Split-Path -Path $AssetsJsonFile
  $relPath = [IO.Path]::GetRelativePath($repoRoot, $assetsJsonPath)
  $assetsRoot = Join-Path -Path $folders[0].FullName -ChildPath $assets.AssetsRepoPrefixPath -AdditionalChildPath $relPath

  return $assetsRoot
}

Function Move-AssetsFromLangRepo {
  param(
    [string] $AssetsRoot
  )
  $filter = $LangRecordingDirs[$language]
  Write-Host "Language recording directory name=$filter"
  Write-Host "Get-ChildItem -Recurse -Filter ""*.json"" | Where-Object { `$_.DirectoryName.Split([IO.Path]::DirectorySeparatorChar) -contains ""$filter"" }"
  $filesToMove = Get-ChildItem -Recurse -Filter "*.json" | Where-Object { $_.DirectoryName.Split([IO.Path]::DirectorySeparatorChar) -contains "$filter" }
  [string] $currentDir = Get-Location
  foreach ($fromFile in $filesToMove) {
    $relPath = [IO.Path]::GetRelativePath($currentDir, $fromFile)
    $toFile = Join-Path -Path $AssetsRoot -ChildPath $relPath
    # Write-Host "Moving from=$fromFile"
    # Write-Host "          to=$toFile"
    $toPath = Split-Path -Path $toFile
    if (!(Test-Path $toPath)) {
      New-Item -Path $toPath -ItemType Directory -Force | Out-Null
    }
    Move-Item -LiteralPath $fromFile -Destination $toFile
  }
}

Function Remove-ProxyAssetsFolder {
  if (![string]::IsNullOrWhitespace($env:DISABLE_INTEGRATION_BRANCH_CLEANUP)) {
    return
  }
  Write-Host "cleaning up $env:PROXY_ASSETS_FOLDER"
  Remove-Item -LiteralPath $env:PROXY_ASSETS_FOLDER -Force -Recurse
}

Test-Exe-In-Path -ExeToLookFor $GitExe
$language = Get-Repo-Language

# If the initial push is being performed, ensure that test-proxy is
# in the path and that we're able to map the language's recording
# directories
if ($InitialPush) {
  Test-Exe-In-Path -ExeToLookFor $TestProxyExe
  Test-TestProxyVersion -TestProxyExe $TestProxyExe
  if (!$LangRecordingDirs.ContainsKey($language)) {
    Write-Error "The language, $language, does not have an entry in the LangRecordingDirs dictionary."
    exit 1
  }
}

# Create the assets-json file
$assetsJsonFile = New-Assets-Json-File -Language $language

# If the initial push is being done:
# 1. Do a restore on the assetsJsonFile, it'll setup the directory that will allow a push to be done
# 2. Move all of the assets over, preserving the directory structure
# 3. Push the repository which will update the assets.json with the new Tag
if ($InitialPush) {

  try {
    $proxyAssetsFolder = Set-ProxyAssetsFolder
    Write-Host "proxyAssetsFolder=$proxyAssetsFolder"

    # Execute a restore on the current assets.json, it'll prep the root directory that
    # the recordings need to be copied into
    $CommandArgs = "restore --assets-json-path $assetsJsonFile"
    Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs

    $assetsRoot = Get-AssetsRoot -AssetsJsonFile $assetsJsonFile
    Write-Host "assetsRoot=$assetsRoot"

    Move-AssetsFromLangRepo -AssetsRoot $assetsRoot

    $CommandArgs = "push --assets-json-path $assetsJsonFile"
    Invoke-ProxyCommand -TestProxyExe $TestProxyExe -CommandArgs $CommandArgs

    # Verify that the assets.json file was updated
    $updatedAssets = Get-Content $assetsJsonFile | Out-String | ConvertFrom-Json
    if ([String]::IsNullOrWhitespace($($updatedAssets.Tag))) {
      Write-Error "AssetsJsonFile ($assetsJsonFile) did not have it's tag updated"
      exit 1
    }
  }
  finally {
    Remove-ProxyAssetsFolder
    $env:PROXY_ASSETS_FOLDER = $OriginalProxyAssetsFolder
  }
}
