<#
.SYNOPSIS
    Checks token counts for skill files to ensure they're within budget.

.DESCRIPTION
    Validates that SKILL.md and reference files are under the recommended token limits.
    Uses the approximation of ~4 characters per token.
    
    Limits per agentskills.io spec:
    - SKILL.md: < 5000 tokens recommended (~500 lines)
    - references/*.md: Keep focused, loaded on-demand

.PARAMETER SkillPath
    Path to a specific skill folder, or omit to check all skills.

.EXAMPLE
    .\check-skill-tokens.ps1
    .\check-skill-tokens.ps1 -SkillPath ".github/skills/typespec-new-project"
#>

param(
    [string]$SkillPath
)

$ErrorActionPreference = "Stop"

# Token limits (from agentskills.io spec and Azure team's CONTRIBUTING.md)
$LIMITS = @{
    "SKILL.md" = @{ Soft = 500; Hard = 5000 }
    "references/*.md" = @{ Soft = 1000; Hard = 2000 }
}

function Get-EstimatedTokens {
    param([string]$Content)
    return [math]::Ceiling($Content.Length / 4)
}

function Test-SkillTokens {
    param([string]$SkillFolder)
    
    $results = @()
    $skillName = Split-Path $SkillFolder -Leaf
    
    # Check SKILL.md
    $skillFile = Join-Path $SkillFolder "SKILL.md"
    if (Test-Path $skillFile) {
        $content = Get-Content $skillFile -Raw
        $tokens = Get-EstimatedTokens $content
        $limit = $LIMITS["SKILL.md"]
        
        $status = if ($tokens -le $limit.Soft) { "✅" }
                  elseif ($tokens -le $limit.Hard) { "⚠️" }
                  else { "❌" }
        
        $results += [PSCustomObject]@{
            Skill = $skillName
            File = "SKILL.md"
            Tokens = $tokens
            SoftLimit = $limit.Soft
            HardLimit = $limit.Hard
            Status = $status
        }
    }
    
    # Check references
    $refsFolder = Join-Path $SkillFolder "references"
    if (Test-Path $refsFolder) {
        Get-ChildItem $refsFolder -Filter "*.md" | ForEach-Object {
            $content = Get-Content $_.FullName -Raw
            $tokens = Get-EstimatedTokens $content
            $limit = $LIMITS["references/*.md"]
            
            $status = if ($tokens -le $limit.Soft) { "✅" }
                      elseif ($tokens -le $limit.Hard) { "⚠️" }
                      else { "❌" }
            
            $results += [PSCustomObject]@{
                Skill = $skillName
                File = "references/$($_.Name)"
                Tokens = $tokens
                SoftLimit = $limit.Soft
                HardLimit = $limit.Hard
                Status = $status
            }
        }
    }
    
    return $results
}

# Main execution
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    $repoRoot = (Get-Location).Path
}

$skillsRoot = Join-Path $repoRoot ".github/skills"

if ($SkillPath) {
    $folders = @(Resolve-Path $SkillPath)
} else {
    $folders = Get-ChildItem $skillsRoot -Directory | 
               Where-Object { $_.Name -ne "_template" -and $_.Name -ne "tests" } |
               Select-Object -ExpandProperty FullName
}

$allResults = @()
foreach ($folder in $folders) {
    $allResults += Test-SkillTokens $folder
}

# Display results
Write-Host "`n📊 Skill Token Budget Check" -ForegroundColor Cyan
Write-Host "═" * 70

$allResults | Format-Table -AutoSize

# Summary
$overSoft = ($allResults | Where-Object { $_.Status -eq "⚠️" }).Count
$overHard = ($allResults | Where-Object { $_.Status -eq "❌" }).Count

Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "  Total files checked: $($allResults.Count)"
Write-Host "  ✅ Under soft limit: $(($allResults | Where-Object { $_.Status -eq '✅' }).Count)"
Write-Host "  ⚠️ Over soft limit: $overSoft"
Write-Host "  ❌ Over hard limit: $overHard"

if ($overHard -gt 0) {
    Write-Host "`n❌ FAIL: Some files exceed hard limits!" -ForegroundColor Red
    exit 1
} elseif ($overSoft -gt 0) {
    Write-Host "`n⚠️ WARNING: Some files exceed soft limits. Consider moving content to references/." -ForegroundColor Yellow
}

