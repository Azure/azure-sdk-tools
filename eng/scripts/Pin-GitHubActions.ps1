#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Finds GitHub Actions pinned to tags and optionally pins them to SHAs.
.DESCRIPTION
    Scans .github/ for uses: references pinned to tags/branches.
    With -Fix, resolves each to a commit SHA via gh CLI and rewrites in-place,
    adding a comment on the line above: # SHA corresponds to action@tag
#>

[CmdletBinding()]
param(
    [string]$Path = ".github",
    [switch]$Fix,
    # Restrict to a specific owner category: GitHub, Azure, Microsoft, 3P, Local
    [ValidateSet("GitHub", "Azure", "Microsoft", "3P", "Local")]
    [string]$OwnerType
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$usesPattern = '(?<pre>\buses:\s*)(?<action>[a-zA-Z0-9_\-\.]+/[a-zA-Z0-9_\-\.]+)@(?<ref>[a-zA-Z0-9_\-\.\/]+)'

function Get-Category([string]$Action) {
    $org = ($Action -split '/')[0].ToLower()
    if ($org -eq 'actions') { return 'GitHub owned' }
    if ($org -eq 'azure') { return 'Azure/ repo owned' }
    if ($org -eq 'microsoft') { return 'Microsoft/ repo owned' }
    return 'third party'
}

function Resolve-ActionSha([string]$Action, [string]$Ref) {
    $org, $repo = $Action -split '/', 2

    # Try tag first, then branch
    foreach ($refType in @("tags", "heads")) {
        try {
            $result = gh api "repos/$org/$repo/git/ref/$refType/$Ref" --jq '.object' 2>$null | ConvertFrom-Json
        } catch { continue }
        if (-not $result -or -not $result.PSObject.Properties['sha']) { continue }

        if ($result.PSObject.Properties['type'] -and $result.type -eq 'tag') {
            return (gh api "repos/$org/$repo/git/tags/$($result.sha)" --jq '.object.sha' 2>$null).Trim()
        }
        return $result.sha
    }
    return $null
}

# Find YAML files
if (-not (Test-Path $Path)) { Write-Error "Path not found: $Path"; exit 1 }
$yamlFiles = Get-ChildItem -Path $Path -Recurse -Include "*.yml", "*.yaml" -File
if (-not $yamlFiles) { Write-Host "No YAML files found in $Path"; exit 0 }

# Parse all uses: references
$refs = foreach ($file in $yamlFiles) {
    $lines = Get-Content $file.FullName
    $relPath = Resolve-Path $file.FullName -Relative -ErrorAction SilentlyContinue
    if (-not $relPath) { $relPath = $file.FullName }

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line.TrimStart().StartsWith('#')) { continue }
        if ($line -match '\buses:\s*\./') {
            [PSCustomObject]@{ File = $relPath; Line = $i + 1; Action = '(local)'; Ref = ($line -replace '.*uses:\s*','').Trim(); Category = 'local'; IsSha = $false }
            continue
        }
        if ($line -match $usesPattern) {
            $ref = $Matches['ref']
            [PSCustomObject]@{ File = $relPath; Line = $i + 1; Action = $Matches['action']; Ref = $ref; Category = Get-Category $Matches['action']; IsSha = ($ref -match '^[0-9a-f]{40}$') }
        }
    }
}

# Map OwnerType to category name for filtering
$ownerCategoryMap = @{
    'GitHub'    = 'GitHub owned'
    'Azure'     = 'Azure/ repo owned'
    'Microsoft' = 'Microsoft/ repo owned'
    '3P'        = 'third party'
    'Local'     = 'local'
}

if ($OwnerType) {
    $refs = @($refs | Where-Object Category -eq $ownerCategoryMap[$OwnerType])
}

if (-not $refs) { Write-Host "No action references found."; exit 0 }

# Display grouped by category
foreach ($cat in @('GitHub owned', 'Azure/ repo owned', 'Microsoft/ repo owned', 'third party', 'local')) {
    $group = @($refs | Where-Object Category -eq $cat)
    if ($group.Count -eq 0) { continue }

    Write-Host "`n=== $cat ===" -ForegroundColor Cyan
    foreach ($r in $group) {
        $pin = if ($r.IsSha) { "pinned" } elseif ($r.Category -eq 'local') { "local" } else { "TAG" }
        if ($pin -eq "TAG") {
            Write-Host "  [$pin] $($r.Action)@$($r.Ref)  ($($r.File):$($r.Line))" -ForegroundColor Red
        } else {
            Write-Host "  [$pin] $($r.Action)@$($r.Ref)  ($($r.File):$($r.Line))"
        }
    }
}

$unpinned = @($refs | Where-Object { -not $_.IsSha -and $_.Category -ne 'local' })
Write-Host "`n$($unpinned.Count) unpinned reference(s) found."

if (-not $Fix) {
    if ($unpinned.Count -gt 0) { Write-Host "Run with -Fix to pin them to SHAs." }
    exit 0
}

# --- Fix mode ---
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "gh CLI is required for -Fix mode. Install from https://cli.github.com/"
    exit 1
}

# Resolve unique action@ref pairs
$shaCache = @{}
$unpinned | Select-Object Action, Ref -Unique | ForEach-Object {
    $key = "$($_.Action)@$($_.Ref)"
    Write-Host "  Resolving $key ... " -NoNewline
    $sha = Resolve-ActionSha $_.Action $_.Ref
    if ($sha) {
        $shaCache[$key] = $sha
        Write-Host $sha -ForegroundColor Green
    } else {
        Write-Host "FAILED" -ForegroundColor Red
    }
}

# Rewrite files
$fixed = 0
foreach ($group in ($unpinned | Group-Object File)) {
    $fullPath = (Resolve-Path $group.Group[0].File).Path
    $lines = [System.Collections.Generic.List[string]](Get-Content $fullPath)
    $changed = $false

    # Process in reverse line order so insertions don't shift later indices
    foreach ($r in ($group.Group | Sort-Object Line -Descending)) {
        $sha = $shaCache["$($r.Action)@$($r.Ref)"]
        if (-not $sha) { continue }
        $idx = $r.Line - 1
        $oldLine = $lines[$idx]
        $newLine = $oldLine -replace "(?<pre>uses:\s*$([regex]::Escape($r.Action)))@$([regex]::Escape($r.Ref))", "`${pre}@$sha"
        if ($newLine -ne $oldLine) {
            $lines[$idx] = $newLine
            $comment = "# SHA corresponds to $($r.Action)@$($r.Ref)"
            $indent = if ($oldLine -match '^(\s*)') { $Matches[1] } else { '' }
            # Update existing comment above if present, otherwise insert
            if ($idx -gt 0 -and $lines[$idx - 1] -match '^\s*# SHA corresponds to') {
                $lines[$idx - 1] = "$indent$comment"
            } else {
                $lines.Insert($idx, "$indent$comment")
            }
            $changed = $true
            $fixed++
        }
    }
    if ($changed) { Set-Content $fullPath $lines }
}

Write-Host "`nFixed $fixed reference(s)."
