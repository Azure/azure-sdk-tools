#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Validates the CODEOWNER_EDITED file against the original CODEOWNERS file.

.DESCRIPTION
    This script performs the following validations:
    1. Counts total number of codeowner entries in both files and ensures they match
    2. Validates that no subpaths appear above their main paths
    3. Identifies any missing or extra entries
    4. Reports on path ordering issues

.PARAMETER OriginalFile
    Path to the original CODEOWNERS file (defaults to CODEOWNERS in the same directory)

.PARAMETER EditedFile
    Path to the edited CODEOWNERS file (defaults to CODEOWNER_EDITED in the same directory)

.EXAMPLE
    .\ValidateCodeowners.ps1
    Validates using default file paths

.EXAMPLE
    .\ValidateCodeowners.ps1 -OriginalFile "CODEOWNERS" -EditedFile "CODEOWNER_EDITED"
    Validates using specified file paths
#>

param(
    [string]$OriginalFile = "CODEOWNERS",
    [string]$EditedFile = "CODEOWNER_EDITED2"
)

# Ensure we're in the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# Check if files exist
if (-not (Test-Path $OriginalFile)) {
    Write-Error "Original file not found: $OriginalFile"
    exit 1
}

if (-not (Test-Path $EditedFile)) {
    Write-Error "Edited file not found: $EditedFile"
    exit 1
}

Write-Host "🔍 Validating CODEOWNERS files..." -ForegroundColor Cyan
Write-Host "📄 Original: $OriginalFile"
Write-Host "📝 Edited: $EditedFile"
Write-Host ""

# Function to extract path entries and owners from a CODEOWNERS file
function Get-PathEntries {
    param([string]$FilePath)
    
    $entries = @()
    $content = Get-Content $FilePath
    
    foreach ($line in $content) {
        $trimmed = $line.Trim()
        
        # Skip comments, empty lines, and service/PR label lines
        if ($trimmed -eq "" -or 
            $trimmed.StartsWith("#") -or
            $trimmed.StartsWith("/*") -and $trimmed.Contains("@") -eq $false) {
            continue
        }
        
        # Look for lines that have paths (start with / and contain @)
        if ($trimmed.StartsWith("/") -and $trimmed.Contains("@")) {
            # Extract the path part (everything before the first @)
            $pathPart = ($trimmed -split "@")[0].Trim()
            # Extract owners (everything after the first @, including the @)
            $ownersPart = "@" + ($trimmed -split "@", 2)[1]
            
            if ($pathPart -ne "") {
                $entries += [PSCustomObject]@{
                    Path = $pathPart
                    Owners = $ownersPart
                    FullLine = $trimmed
                    LineNumber = $content.IndexOf($line) + 1
                }
            }
        }
    }
    
    return $entries
}

# Function to extract all owner references from a CODEOWNERS file with context
function Get-AllOwnersWithContext {
    param([string]$FilePath)
    
    $ownersWithContext = @()
    $content = Get-Content $FilePath
    
    for ($i = 0; $i -lt $content.Length; $i++) {
        $line = $content[$i]
        $trimmed = $line.Trim()
        
        # Skip comments that don't contain owners, empty lines
        if ($trimmed -eq "" -or 
            ($trimmed.StartsWith("#") -and -not $trimmed.Contains("@"))) {
            continue
        }
        
        # Extract all @ references from the line
        $matches = [regex]::Matches($trimmed, '@[^\s]+')
        foreach ($match in $matches) {
            $owner = $match.Value
            
            # Find context - look for the most relevant nearby context
            $context = "Unknown"
            
            # If the current line is a path line, use it as context
            if ($trimmed.StartsWith("/") -and $trimmed.Contains("@")) {
                $pathPart = ($trimmed -split "@")[0].Trim()
                $context = "Path: $pathPart"
            }
            # If current line has ServiceLabel or PRLabel, use it
            elseif ($trimmed -match "# ServiceLabel: %(.+)") {
                $context = "ServiceLabel: %" + $Matches[1]
            }
            elseif ($trimmed -match "# PRLabel: %(.+)") {
                $context = "PRLabel: %" + $Matches[1]
            }
            # Otherwise, look backwards for the closest relevant context (not forwards)
            else {
                for ($j = $i - 1; $j -ge [math]::Max(0, $i - 10); $j--) {
                    $contextLine = $content[$j].Trim()
                    if ($contextLine -match "# ServiceLabel: %(.+)") {
                        $context = "ServiceLabel: %" + $Matches[1]
                        break
                    } elseif ($contextLine -match "# PRLabel: %(.+)") {
                        $context = "PRLabel: %" + $Matches[1]
                        break
                    } elseif ($contextLine.StartsWith("/") -and $contextLine.Contains("@")) {
                        # Extract path
                        $pathPart = ($contextLine -split "@")[0].Trim()
                        $context = "Path: $pathPart"
                        break
                    }
                    # Stop if we hit a section boundary
                    elseif ($contextLine.StartsWith("##") -or $contextLine.StartsWith("# ###")) {
                        break
                    }
                }
            }
            
            $ownersWithContext += [PSCustomObject]@{
                Owner = $owner
                LineNumber = $i + 1
                Context = $context
                FullLine = $trimmed
            }
        }
    }
    
    return $ownersWithContext
}

# Function to check if one path is a subpath of another
function Test-IsSubPath {
    param([string]$PotentialSubPath, [string]$MainPath)
    
    # Normalize paths by removing trailing slashes and wildcards
    $normalizedSub = $PotentialSubPath.TrimEnd('/', '*')
    $normalizedMain = $MainPath.TrimEnd('/', '*')
    
    # Check if the subpath starts with the main path
    return $normalizedSub.StartsWith($normalizedMain) -and $normalizedSub -ne $normalizedMain
}

# Extract entries from both files
Write-Host "📊 Analyzing files..." -ForegroundColor Yellow

$originalEntries = Get-PathEntries $OriginalFile
$editedEntries = Get-PathEntries $EditedFile

$originalOwnersWithContext = Get-AllOwnersWithContext $OriginalFile
$editedOwnersWithContext = Get-AllOwnersWithContext $EditedFile

$originalOwners = $originalOwnersWithContext | ForEach-Object { $_.Owner } | Sort-Object | Get-Unique
$editedOwners = $editedOwnersWithContext | ForEach-Object { $_.Owner } | Sort-Object | Get-Unique

Write-Host "📈 Entry counts:"
Write-Host "   Original file: $($originalEntries.Count) path entries"
Write-Host "   Edited file: $($editedEntries.Count) path entries"
Write-Host ""
Write-Host "👥 Owner counts:"
Write-Host "   Original file: $($originalOwners.Count) unique owners"
Write-Host "   Edited file: $($editedOwners.Count) unique owners"

$validationResults = @{
    CountMatch = $false
    OwnerCountMatch = $false
    PathOrderingIssues = @()
    MissingEntries = @()
    ExtraEntries = @()
    MissingOwners = @()
    ExtraOwners = @()
    TotalIssues = 0
}

# 1. Check entry count
if ($originalEntries.Count -eq $editedEntries.Count) {
    Write-Host "✅ Entry count matches!" -ForegroundColor Green
    $validationResults.CountMatch = $true
} else {
    Write-Host "❌ Entry count mismatch!" -ForegroundColor Red
    $validationResults.TotalIssues++
}

# 1b. Check owner count
if ($originalOwners.Count -eq $editedOwners.Count) {
    Write-Host "✅ Owner count matches!" -ForegroundColor Green
    $validationResults.OwnerCountMatch = $true
} else {
    Write-Host "❌ Owner count mismatch!" -ForegroundColor Red
    $validationResults.TotalIssues++
}

# 2. Check for missing or extra entries
Write-Host ""
Write-Host "🔍 Checking for missing/extra entries..." -ForegroundColor Yellow

$originalPaths = $originalEntries | ForEach-Object { $_.Path }
$editedPaths = $editedEntries | ForEach-Object { $_.Path }

$missingPaths = $originalPaths | Where-Object { $_ -notin $editedPaths }
$extraPaths = $editedPaths | Where-Object { $_ -notin $originalPaths }

if ($missingPaths.Count -gt 0) {
    Write-Host "❌ Missing entries in edited file:" -ForegroundColor Red
    $missingPaths | ForEach-Object { 
        Write-Host "   - $_" -ForegroundColor Red 
        $validationResults.MissingEntries += $_
    }
    $validationResults.TotalIssues++
} else {
    Write-Host "✅ No missing entries" -ForegroundColor Green
}

if ($extraPaths.Count -gt 0) {
    Write-Host "❌ Extra entries in edited file:" -ForegroundColor Red
    $extraPaths | ForEach-Object { 
        Write-Host "   - $_" -ForegroundColor Red 
        $validationResults.ExtraEntries += $_
    }
    $validationResults.TotalIssues++
} else {
    Write-Host "✅ No extra entries" -ForegroundColor Green
}

# 2b. Check for missing or extra owners
Write-Host ""
Write-Host "🔍 Checking for missing/extra owners..." -ForegroundColor Yellow

$missingOwners = $originalOwners | Where-Object { $_ -notin $editedOwners }
$extraOwners = $editedOwners | Where-Object { $_ -notin $originalOwners }

if ($missingOwners.Count -gt 0) {
    Write-Host "❌ Missing owners in edited file:" -ForegroundColor Red
    $missingOwners | ForEach-Object { 
        $missingOwner = $_
        $originalContext = $originalOwnersWithContext | Where-Object { $_.Owner -eq $missingOwner } | Select-Object -First 1
        if ($originalContext) {
            Write-Host "   - $missingOwner" -ForegroundColor Red
            Write-Host "     Original location: Line $($originalContext.LineNumber) - $($originalContext.Context)" -ForegroundColor Yellow
        } else {
            Write-Host "   - $missingOwner (context not found)" -ForegroundColor Red
        }
        $validationResults.MissingOwners += $missingOwner
    }
    $validationResults.TotalIssues++
} else {
    Write-Host "✅ No missing owners" -ForegroundColor Green
}

if ($extraOwners.Count -gt 0) {
    Write-Host "❌ Extra owners in edited file:" -ForegroundColor Red
    $extraOwners | ForEach-Object { 
        Write-Host "   - $_" -ForegroundColor Red 
        $validationResults.ExtraOwners += $_
    }
    $validationResults.TotalIssues++
} else {
    Write-Host "✅ No extra owners" -ForegroundColor Green
}

# 3. Check path ordering (subpaths should not appear above main paths)
Write-Host ""
Write-Host "🔍 Checking path ordering..." -ForegroundColor Yellow

$orderingIssues = @()

for ($i = 0; $i -lt $editedEntries.Count; $i++) {
    $currentEntry = $editedEntries[$i]
    
    # Check if any entry below this one is a parent path
    for ($j = $i + 1; $j -lt $editedEntries.Count; $j++) {
        $laterEntry = $editedEntries[$j]
        
        # If current entry is a subpath of a later entry, that's a problem
        if (Test-IsSubPath $currentEntry.Path $laterEntry.Path) {
            $issue = [PSCustomObject]@{
                SubPath = $currentEntry.Path
                MainPath = $laterEntry.Path
                SubPathLine = $currentEntry.LineNumber
                MainPathLine = $laterEntry.LineNumber
                Description = "Subpath '$($currentEntry.Path)' appears before main path '$($laterEntry.Path)'"
            }
            $orderingIssues += $issue
            $validationResults.PathOrderingIssues += $issue
        }
    }
}

if ($orderingIssues.Count -gt 0) {
    Write-Host "❌ Path ordering issues found:" -ForegroundColor Red
    $orderingIssues | ForEach-Object {
        Write-Host "   - $($_.Description)" -ForegroundColor Red
        Write-Host "     Subpath at line $($_.SubPathLine): $($_.SubPath)" -ForegroundColor Red
        Write-Host "     Main path at line $($_.MainPathLine): $($_.MainPath)" -ForegroundColor Red
        Write-Host ""
    }
    $validationResults.TotalIssues++
} else {
    Write-Host "✅ Path ordering is correct" -ForegroundColor Green
}

# 4. Summary
Write-Host ""
Write-Host "📋 Validation Summary:" -ForegroundColor Cyan
Write-Host "======================"

if ($validationResults.TotalIssues -eq 0) {
    Write-Host "🎉 All validations passed! The edited file is correct." -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Found $($validationResults.TotalIssues) issue(s):" -ForegroundColor Red
    
    if (-not $validationResults.CountMatch) {
        Write-Host "   • Entry count mismatch" -ForegroundColor Red
    }
    
    if (-not $validationResults.OwnerCountMatch) {
        Write-Host "   • Owner count mismatch" -ForegroundColor Red
    }
    
    if ($validationResults.MissingEntries.Count -gt 0) {
        Write-Host "   • $($validationResults.MissingEntries.Count) missing entries" -ForegroundColor Red
    }
    
    if ($validationResults.ExtraEntries.Count -gt 0) {
        Write-Host "   • $($validationResults.ExtraEntries.Count) extra entries" -ForegroundColor Red
    }
    
    if ($validationResults.MissingOwners.Count -gt 0) {
        Write-Host "   • $($validationResults.MissingOwners.Count) missing owners" -ForegroundColor Red
    }
    
    if ($validationResults.ExtraOwners.Count -gt 0) {
        Write-Host "   • $($validationResults.ExtraOwners.Count) extra owners" -ForegroundColor Red
    }
    
    if ($validationResults.PathOrderingIssues.Count -gt 0) {
        Write-Host "   • $($validationResults.PathOrderingIssues.Count) path ordering issues" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "Please fix the issues above before proceeding." -ForegroundColor Yellow
    exit 1
}
