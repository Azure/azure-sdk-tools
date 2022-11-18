[CmdletBinding()]
Param (
  [Parameter(Mandatory=$True)]
  [array] $ArtifactList,
  [Parameter(Mandatory=$True)]
  [string] $ArtifactPath,
  [Parameter(Mandatory=$True)]
  [string] $APIViewUri,
  [Parameter(Mandatory=$True)]
  [string] $APIKey,
  [Parameter(Mandatory=$True)]
  [string] $SourceBranch,
  [Parameter(Mandatory=$True)]
  [string] $DefaultBranch,
  [Parameter(Mandatory=$True)]
  [string] $ConfigFileDir,
  [Parameter(Mandatory=$True)]
  [string] $buildId,
  [Parameter(Mandatory=$True)]
  [string] $repoName,
  [Parameter(Mandatory=$True)]
  [string] $Language
)