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

function Get-PackageName {
    param (
        [Parameter(Mandatory = $true)] [string]$CsprojPath
    )
    try {
        [xml]$xml = Get-Content -LiteralPath $CsprojPath -Raw
    }
    catch {
        Write-Warning "Failed to parse csproj for package name: $($_.Exception.Message)"
        return [System.IO.Path]::GetFileNameWithoutExtension($CsprojPath)
    }

    # NuGet resolution order: PackageId > AssemblyName > project filename
    foreach ($element in @('PackageId', 'AssemblyName')) {
        $node = $xml.SelectSingleNode("//PropertyGroup/$element")
        if ($node -and $node.InnerText.Trim()) { return $node.InnerText.Trim() }
    }

    return [System.IO.Path]::GetFileNameWithoutExtension($CsprojPath)
}

function Find-RepoRoot {
    param (
        [Parameter(Mandatory = $true)] [string]$StartDirectory
    )
    $dir = (Get-Item -LiteralPath $StartDirectory).FullName
    while ($dir) {
        if (Test-Path (Join-Path $dir ".git")) { return $dir }
        $parent = Split-Path $dir -Parent
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    return $null
}

function Get-CentralPackageManagementFiles {
    param (
        [Parameter(Mandatory = $true)] [string]$RepoRoot
    )
    $files = @()

    # New CPM structure (post CPM migration): eng/centralpackagemanagement/*.Packages.props
    # Only files directly in this directory — NOT in overrides/ subdirectory
    $cpmDir = Join-Path $RepoRoot "eng" "centralpackagemanagement"
    if (Test-Path -LiteralPath $cpmDir -PathType Container) {
        $found = Get-ChildItem -LiteralPath $cpmDir -File -Filter '*.Packages.props'
        if ($found) { $files += $found }
    }

    # Old format (pre CPM migration): eng/Packages.Data.props
    $oldFile = Join-Path $RepoRoot "eng" "Packages.Data.props"
    if (Test-Path -LiteralPath $oldFile -PathType Leaf) {
        $files += Get-Item -LiteralPath $oldFile
    }

    return $files
}

function Update-CentralPackageVersions {
    param (
        [Parameter(Mandatory = $true)] [string]$PackageName,
        [Parameter(Mandatory = $true)] [string]$NewVersion,
        [Parameter(Mandatory = $true)] [object[]]$Files
    )

    $totalUpdates = 0
    $escapedName = [regex]::Escape($PackageName)

    # Match PackageVersion (Include|Update) and PackageReference (Update) entries with literal versions.
    # The version must start with a digit to skip MSBuild property references like $(SomeVersion).
    $pattern = '(<Package(?:Version|Reference)\s+(?:Include|Update)="' + $escapedName + '"\s+Version=")(\d[^"]*?)(")'

    foreach ($file in $Files) {
        $content = Get-Content -LiteralPath $file.FullName -Raw
        if (-not $content) { continue }
        $newContent = [regex]::Replace($content, $pattern, '${1}' + $NewVersion + '${3}')

        if ($newContent -ne $content) {
            Set-Content -LiteralPath $file.FullName -Value $newContent -NoNewline
            Write-Host "Updated package '$PackageName' version to '$NewVersion' in '$($file.FullName)'"
            $totalUpdates++
        }
    }

    return $totalUpdates
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

  # Update Central Package Management files with the released version so
  # other packages that depend on this one pick up the latest release.
  # Wrapped in its own try/catch so a CPM failure never blocks the version bump PR.
  try {
    $packageName = Get-PackageName -CsprojPath $csprojPath
    Write-Host "Package name: $packageName"

    $repoRoot = Find-RepoRoot -StartDirectory $ToolDirectory
    if ($repoRoot) {
      Write-Host "Repository root: $repoRoot"
      $cpmFiles = Get-CentralPackageManagementFiles -RepoRoot $repoRoot
      if ($cpmFiles.Count -gt 0) {
        Write-Host "Found $($cpmFiles.Count) central package management file(s)"
        $updatedCount = Update-CentralPackageVersions -PackageName $packageName -NewVersion $currentVersion -Files $cpmFiles
        Write-Host "Updated $updatedCount central package management file(s)"
      } else {
        Write-Host "No central package management files found - skipping CPM update"
      }
    } else {
      Write-Host "Could not determine repository root - skipping CPM update"
    }
    Write-Host "##vso[task.setvariable variable=CpmUpdateSuccess]true"
  }
  catch {
    Write-Warning "CPM update failed — central package management will need to be updated manually: $($_.Exception.Message)"
    Write-Host "##vso[task.logissue type=warning]CPM update failed for package version bump. Central package management files will need a manual update."
    Write-Host "##vso[task.setvariable variable=CpmUpdateSuccess]false"
  }

  Write-Host "##vso[task.setvariable variable=NewVersion]$newVersion"

  Write-Host "NewVersion=$newVersion"
} catch {
  Write-Error $_.Exception.Message
  exit 1
}
