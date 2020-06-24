# Common Changelog Operations

$RELEASE_TITLE_REGEX = "(?<releaseNoteTitle>^\#+.*(?<version>\b\d+\.\d+\.\d+([^0-9\s][^\s:]+)?)(\s(?<releaseStatus>\(Unreleased\)|\(\d{4}-\d{2}-\d{2}\)))?)"

# given a CHANGELOG.md file, extract the relevant info we need to decorate a release
function Get-ReleaseNotes {
    param (
        [Parameter(Mandatory = $true)]
        [String]$ChangeLogLocation
    )
    
    $ErrorActionPreference = 'Stop'

    $releaseNotes = @{}
    if ($ChangeLogLocation.Length -eq 0)
    {
        return $releaseNotes
    }
    
    try 
    {
        $contents = Get-Content $ChangeLogLocation
        # walk the document, finding where the version specifiers are and creating lists
        $releaseNotesEntry = $null
        foreach($line in $contents){
            if ($line -match $RELEASE_TITLE_REGEX)
            {
               $releaseNotesEntry = [pscustomobject]@{ 
                  ReleaseVersion = $matches["version"]
                  ReleaseStatus = $matches["releaseStatus"]
                  ReleaseTitle = $line
                  ReleaseContent = @() # Release content without the version title
               }
               $releaseNotes[$releaseNotesEntry.ReleaseVersion] = $releaseNotesEntry
            }
            else
            {
               if ($releaseNotesEntry) {
                  $releaseNotesEntry.ReleaseContent += $line
               }
            }
        }
    }
    catch
    {
        Write-Host "Error parsing $ChangeLogLocation."
        Write-Host $_.Exception.Message
    }
    return $releaseNotes
}

function Get-ReleaseNote {
   param (
      [Parameter(Mandatory = $true)]
      [String]$ChangeLogLocation,
      [Parameter(Mandatory = $true)]
      [String]$VersionString
   )

   $releaseNotes = Get-ReleaseNotes -ChangeLogLocation $ChangeLogLocation

   if ($releaseNotes.ContainsKey($VersionString)) 
   {
         return $releaseNotes[$VersionString]
   }
   Write-Error "Release Notes for the Specified version ${VersionString} was not found"
   exit 1
}

function Confirm-ChangeLog {
   param (
      [Parameter(Mandatory = $true)]
      [String]$ChangeLogLocation,
      [Parameter(Mandatory = $true)]
      [String]$VersionString,
      [boolean]$ForRelease=$false
   )

   $ReleaseNotes = Get-ReleaseNote -ChangeLogLocation $ChangeLogLocation -VersionString $VersionString

   if ([System.String]::IsNullOrEmpty($ReleaseNotes.ReleaseStatus))
   {
      Write-Host ("##[error]Changelog '{0}' has wrong release note title" -f $ChangeLogLocation)
      Write-Host "##[info]Ensure the release date is included i.e. (yyyy-MM-dd) or (Unreleased) if not yet released"
      exit 1
   }

   if ($ForRelease -eq $True)
   {
      $CurrentDate = Get-Date -Format "yyyy-MM-dd"
      if ($ReleaseNotes.ReleaseStatus -ne "($CurrentDate)")
      {
         Write-Host ("##[warning]Incorrect Date: Please use the current date in the Changelog '{0}' before releasing the package" -f $ChangeLogLocation)
         exit 1
      }

      if ([System.String]::IsNullOrWhiteSpace($ReleaseNotes.ReleaseContent))
      {
         Write-Host ("##[error]Empty Release Notes for '{0}' in '{1}'" -f $VersionString, $ChangeLogLocation)
         Write-Host "##[info]Please ensure there is a release notes entry before releasing the package."
         exit 1
      }
   }

   Write-Host ($ReleaseNotes | Format-Table | Out-String)
}
 
Export-ModuleMember -Function 'Get-ReleaseNotes'
Export-ModuleMember -Function 'Get-ReleaseNote'
Export-ModuleMember -Function 'Confirm-ChangeLog'