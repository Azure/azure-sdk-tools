param(
  [Parameter(Mandatory = $true)]
  [string]$ToolDirectory
)

. (Join-Path $PSScriptRoot ".." "common" "scripts" "SemVer.ps1")

function IncrementPackageVersion {
    param (
        [Parameter(Mandatory = $true)] [string]$OldVersion
    )

    if (-not $OldVersion)
    {
      throw "OldVersion is empty"
    }

    $sv = [AzureEngSemanticVersion]::ParseVersionString($OldVersion)

    if (-not $sv)
    {
      throw "Failed to parse version string '$OldVersion'"
    }

    $sv.Patch = $sv.Patch + 1

    return $sv.ToString()
}

function Get-CsprojFileDirectlyUnder {
    param (
        [Parameter(Mandatory = $true)] [string]$Directory
    )

    if (-not (Test-Path -LiteralPath $Directory -PathType Container))
    {
        throw "ToolDirectory '$Directory' does not exist or is not a directory"
    }

    $dirFull = (Get-Item -LiteralPath $Directory).FullName
    $csprojFiles = Get-ChildItem -LiteralPath $Directory -File -Filter '*.csproj' `
      | Where-Object { $_.DirectoryName -eq $dirFull }

    if ($csprojFiles.Count -eq 0)
    {
        throw "No .csproj file found directly under '$Directory'"
    }
    if ($csprojFiles.Count -gt 1)
    {
        throw "Multiple .csproj files found directly under '$Directory'. Please ensure only one csproj is present. Found: $($csprojFiles | ForEach-Object { $_.Name } -join ', ')"
    }

    return $csprojFiles[0].FullName
}

function Get-CsprojVersion {
    param (
        [Parameter(Mandatory = $true)] [string]$CsprojPath
    )
    Write-Host "Discovering current version in directory: $CsprojPath"
    try {
        [xml]$xml = Get-Content -LiteralPath $CsprojPath -Raw
    }
    catch {
        throw "Failed to parse csproj as XML: $CsprojPath`n$($_.Exception.Message)"
    }

    # Try Version then VersionPrefix in any PropertyGroup
    $nodes = @()
    $nodes += $xml.SelectNodes('//PropertyGroup/Version') 2>$null
    $nodes += $xml.SelectNodes('//PropertyGroup/VersionPrefix') 2>$null

    foreach ($n in $nodes)
    {
        if ($n -and $n.InnerText.Trim()) { return $n.InnerText.Trim() }
    }

    throw "No Version or VersionPrefix element with a literal value was found in '$CsprojPath'"
}

function Update-CsprojVersion {
    param (
        [Parameter(Mandatory = $true)] [string]$CsprojPath,
        [Parameter(Mandatory = $true)] [string]$OldVersion,
        [Parameter(Mandatory = $true)] [string]$NewVersion
    )

    try {
        $content = Get-Content -LiteralPath $CsprojPath -Raw
        $updatedContent = $content.Replace($OldVersion, $NewVersion)

        if ($content -eq $updatedContent) {
            Write-Warning "No replacement made - old version '$OldVersion' not found as text in csproj"
        } else {
            Write-Host "Updating version from '$OldVersion' to '$NewVersion'"
            Set-Content -LiteralPath $CsprojPath -Value $updatedContent -NoNewline
            Write-Host "Successfully updated version in '$CsprojPath'"
        }
    }
    catch {
        throw "Failed to update csproj file: $CsprojPath`n$($_.Exception.Message)"
    }
}

try {
  if (-not (Test-Path -LiteralPath $ToolDirectory -PathType Container)) {
    throw "ToolDirectory '$ToolDirectory' does not exist or is not a directory"
  }

  $csprojPath = Get-CsprojFileDirectlyUnder -Directory $ToolDirectory
  Write-Host "Found csproj: $csprojPath"

  $currentVersion = Get-CsprojVersion -CsprojPath $csprojPath
  Write-Host "Current version detected: $currentVersion"

  $newVersion = IncrementPackageVersion -OldVersion $currentVersion
  Write-Host "Computed new version: $newVersion"

  Update-CsprojVersion -CsprojPath $csprojPath -OldVersion $currentVersion -NewVersion $newVersion

  Write-Host "##vso[task.setvariable variable=NewVersion]$newVersion"

  Write-Host "NewVersion=$newVersion"
} catch {
  Write-Error $_.Exception.Message
  exit 1
}
