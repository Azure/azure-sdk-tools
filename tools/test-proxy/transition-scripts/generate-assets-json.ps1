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

class Assets {
  [string]$AssetsRepo = "Azure/azure-sdk-assets"
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

Function Test-Exe-In-Path {
  Param([string] $ExeToLookFor)
  if ($null -eq (Get-Command $ExeToLookFor -ErrorAction SilentlyContinue)) {
    Write-Error "Unable to find $ExeToLookFor in your PATH"
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
      #Return match from group 1 which will be the language, pulled from the repository
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

Test-Exe-In-Path -ExeToLookFor "git"
$AssetsRepoPrefixPath = Get-Repo-Language

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
