try {
    $OriginalFile = "CODEOWNERS"
    $EditedFile = "CODEOWNER_EDITED"
    
    Write-Host "🔍 Generating simplified Git diff analysis..." -ForegroundColor Green
    Write-Host "📄 Original: $OriginalFile"
    Write-Host "📝 New version: $EditedFile"
    
    # Copy edited file as temp CODEOWNERS for git diff
    Copy-Item $EditedFile "CODEOWNERS_TEMP"
    
    Write-Host "📊 Running git diff..."
    $gitDiffOutput = git diff --no-index $OriginalFile CODEOWNERS_TEMP
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "⚠️  No differences detected by git diff" -ForegroundColor Yellow
        $gitDiffOutput = @("Files are identical")
    }
    
    # Parse the diff output
    $addedLines = @()
    $removedLines = @()
    $currentLineOriginal = 0
    $currentLineNew = 0
    
    foreach ($line in $gitDiffOutput) {
        if ($line -match '^@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?') {
            $currentLineOriginal = [int]$Matches[1]
            $currentLineNew = [int]$Matches[3]
        }
        elseif ($line.StartsWith('-') -and -not $line.StartsWith('---')) {
            $removedLines += [PSCustomObject]@{
                LineNumber = $currentLineOriginal
                Content = $line.Substring(1)
            }
            $currentLineOriginal++
        }
        elseif ($line.StartsWith('+') -and -not $line.StartsWith('+++')) {
            $addedLines += [PSCustomObject]@{
                LineNumber = $currentLineNew
                Content = $line.Substring(1)
            }
            $currentLineNew++
        }
        elseif (-not $line.StartsWith('@@') -and -not $line.StartsWith('diff') -and -not $line.StartsWith('index') -and -not $line.StartsWith('---') -and -not $line.StartsWith('+++')) {
            $currentLineOriginal++
            $currentLineNew++
        }
    }
    
    # Clean up temp file
    Remove-Item "CODEOWNERS_TEMP" -ErrorAction SilentlyContinue
    
    Write-Host "📈 Stats: $($removedLines.Count) removed, $($addedLines.Count) added, net: $($addedLines.Count - $removedLines.Count)"
    
    # Content analysis
    $removedContent = $removedLines | ForEach-Object { $_.Content.Trim() } | Where-Object { $_ -ne "" }
    $addedContent = $addedLines | ForEach-Object { $_.Content.Trim() } | Where-Object { $_ -ne "" }
    
    $lostContent = @()
    $preservedContent = @()
    $newContent = @()
    
    foreach ($removed in $removedContent) {
        if ($addedContent -contains $removed) {
            $preservedContent += $removed
        } else {
            $lostContent += $removed
        }
    }
    
    foreach ($added in $addedContent) {
        if ($removedContent -notcontains $added) {
            $newContent += $added
        }
    }

    # Validation checks on the edited file
    Write-Host "🔍 Running validation checks..." -ForegroundColor Yellow
    
    $editedContent = Get-Content $EditedFile
    $validationIssues = @()
    $pathLines = @()
    $duplicatePaths = @()
    
    # Extract all path definitions (lines that don't start with #)
    foreach ($line in $editedContent) {
        $trimmed = $line.Trim()
        if ($trimmed -ne "" -and -not $trimmed.StartsWith("#")) {
            # Extract path pattern (first part before whitespace/owners)
            if ($trimmed -match '^([^\s]+)') {
                $pathPattern = $Matches[1]
                $pathLines += [PSCustomObject]@{
                    Pattern = $pathPattern
                    FullLine = $line
                    LineNumber = [array]::IndexOf($editedContent, $line) + 1
                }
            }
        }
    }
    
    # Check for duplicate paths
    $pathGroups = $pathLines | Group-Object Pattern
    foreach ($group in $pathGroups) {
        if ($group.Count -gt 1) {
            $duplicatePaths += $group.Name
            $validationIssues += "Duplicate path pattern: $($group.Name) (appears $($group.Count) times)"
        }
    }
    
    # Check for malformed lines
    $malformedLines = @()
    for ($i = 0; $i -lt $editedContent.Count; $i++) {
        $line = $editedContent[$i]
        $trimmed = $line.Trim()
        
        # Skip empty lines and comments
        if ($trimmed -eq "" -or $trimmed.StartsWith("#")) { continue }
        
        # Check if line has proper format (path + owners)
        if (-not ($trimmed -match '^\S+\s+@')) {
            $malformedLines += "Line $($i + 1): '$line'"
            $validationIssues += "Malformed line $($i + 1): Missing @ owners"
        }
        
        # Check for common formatting issues
        if ($trimmed -match '@@') {
            $validationIssues += "Line $($i + 1): Double @@ symbols found"
        }
        if ($trimmed -match '#\s*ServiceLabel:\s*%.*\s\s+') {
            $validationIssues += "Line $($i + 1): Extra spaces in ServiceLabel"
        }
    }
    
    Write-Host "📊 Validation: $($pathLines.Count) paths, $($duplicatePaths.Count) duplicates, $($validationIssues.Count) issues" -ForegroundColor Cyan
    
    # Generate summary
    $summary = @"
# Git Diff Summary

Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

## Statistics
- Lines removed: $($removedLines.Count)
- Lines added: $($addedLines.Count)  
- Net change: $($addedLines.Count - $removedLines.Count)
- Content preserved (moved): $($preservedContent.Count)
- Content lost: $($lostContent.Count)
- New content: $($newContent.Count)

## Validation Results
- Total path patterns: $($pathLines.Count)
- Duplicate paths: $($duplicatePaths.Count)
- Validation issues: $($validationIssues.Count)

"@

    if ($validationIssues.Count -eq 0) {
        $summary += "✅ **No validation issues found** - File appears properly organized`n"
    } else {
        $summary += "⚠️ **Validation Issues Found:**`n"
        foreach ($issue in $validationIssues) {
            $summary += "- $issue`n"
        }
    }

    if ($duplicatePaths.Count -gt 0) {
        $summary += "`n### Duplicate Paths ($($duplicatePaths.Count) patterns)`n"
        foreach ($dupPath in $duplicatePaths) {
            $locations = ($pathLines | Where-Object { $_.Pattern -eq $dupPath }).LineNumber -join ", "
            $summary += "- ``$dupPath`` (lines: $locations)`n"
        }
    }

    $summary += @"

## Removed Content ($($lostContent.Count) lines)
"@

    if ($lostContent.Count -eq 0) {
        $summary += "`nNone"
    } else {
        foreach ($lost in $lostContent) {
            # Find the line number where this content was removed
            $removedLineInfo = $removedLines | Where-Object { $_.Content.Trim() -eq $lost } | Select-Object -First 1
            if ($removedLineInfo) {
                $summary += "`n- Line $($removedLineInfo.LineNumber): ``$lost``"
            } else {
                $summary += "`n- ``$lost``"
            }
        }
    }

    $summary += "`n`n## Added Content ($($newContent.Count) lines)"
    
    if ($newContent.Count -eq 0) {
        $summary += "`nNone"
    } else {
        foreach ($new in $newContent) {
            # Find the line number where this content was added
            $addedLineInfo = $addedLines | Where-Object { $_.Content.Trim() -eq $new } | Select-Object -First 1
            if ($addedLineInfo) {
                $summary += "`n- Line $($addedLineInfo.LineNumber): ``$new``"
            } else {
                $summary += "`n- ``$new``"
            }
        }
    }

    # Write files
    $summary | Out-File -FilePath "DIFF_SUMMARY.md" -Encoding UTF8
    
    # Removed lines report
    $removedReport = "# Removed Lines ($($removedLines.Count) total)`n`n"
    if ($removedLines.Count -gt 0) {
        $removedReport += "| Line | Content |`n|------|---------|`n"
        foreach ($removed in ($removedLines | Sort-Object LineNumber)) {
            $content = $removed.Content -replace '\|', '\|' -replace '`', '\`'
            $removedReport += "| $($removed.LineNumber) | ``$content`` |`n"
        }
    }
    $removedReport | Out-File -FilePath "DIFF_REMOVED.md" -Encoding UTF8
    
    # Added lines report  
    $addedReport = "# Added Lines ($($addedLines.Count) total)`n`n"
    if ($addedLines.Count -gt 0) {
        $addedReport += "| Line | Content |`n|------|---------|`n"
        foreach ($added in ($addedLines | Sort-Object LineNumber)) {
            $content = $added.Content -replace '\|', '\|' -replace '`', '\`'
            $addedReport += "| $($added.LineNumber) | ``$content`` |`n"
        }
    }
    $addedReport | Out-File -FilePath "DIFF_ADDED.md" -Encoding UTF8
    
    Write-Host "✅ Reports generated!" -ForegroundColor Green
    Write-Host "📋 DIFF_SUMMARY.md - Stats and content comparison"
    Write-Host "📋 DIFF_REMOVED.md - All removed lines"  
    Write-Host "📋 DIFF_ADDED.md - All added lines"

} catch {
    Write-Error "Error: $($_.Exception.Message)"
    Remove-Item "CODEOWNERS_TEMP" -ErrorAction SilentlyContinue
}
