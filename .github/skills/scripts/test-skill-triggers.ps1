<#
.SYNOPSIS
    Tests Skill discoverability using Azure OpenAI embeddings.

.DESCRIPTION
    Validates that user prompts correctly match Skill descriptions using
    the same embedding similarity approach as PromptToToolMatchEvaluator.

.PARAMETER SkillName
    Name of the skill to test (folder name under .github/skills/).

.PARAMETER MinConfidence
    Minimum cosine similarity score to pass (default: 0.4 = 40%).

.EXAMPLE
    .\test-skill-triggers.ps1 -SkillName typespec-new-project
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SkillName,
    
    [double]$MinConfidence = 0.4
)

$ErrorActionPreference = "Stop"

# Configuration
$EmbeddingModel = "text-embedding-3-large"
$Endpoint = $env:AZURE_OPENAI_ENDPOINT
if (-not $Endpoint) {
    $Endpoint = "https://openai-shared.openai.azure.com/"
}

# Find repo root
$RepoRoot = git rev-parse --show-toplevel 2>$null
if (-not $RepoRoot) {
    $RepoRoot = (Get-Location).Path
}

$SkillsRoot = Join-Path $RepoRoot ".github/skills"
$SkillPath = Join-Path $SkillsRoot $SkillName
$SkillFile = Join-Path $SkillPath "SKILL.md"
$PromptsFile = Join-Path $SkillsRoot "tests/$SkillName/prompts.json"

# Validate files exist
if (-not (Test-Path $SkillFile)) {
    Write-Error "SKILL.md not found at: $SkillFile"
    exit 1
}
if (-not (Test-Path $PromptsFile)) {
    Write-Error "prompts.json not found at: $PromptsFile"
    exit 1
}

# Parse SKILL.md frontmatter to extract description
function Get-SkillDescription {
    param([string]$Path)
    
    $content = Get-Content $Path -Raw
    $lines = $content -split "`n"
    
    $inFrontmatter = $false
    $inDescription = $false
    $description = ""
    
    foreach ($line in $lines) {
        if ($line.Trim() -eq "---") {
            if ($inFrontmatter) { break }
            $inFrontmatter = $true
            continue
        }
        
        if ($inFrontmatter) {
            if ($line -match "^description:\s*\|?\s*$") {
                $inDescription = $true
                continue
            }
            elseif ($line -match "^description:\s*(.+)$") {
                return $Matches[1].Trim()
            }
            elseif ($inDescription) {
                if ($line -match "^\s+(.+)$") {
                    $description += $Matches[1].Trim() + " "
                }
                elseif ($line -match "^[a-z]+:") {
                    break
                }
            }
        }
    }
    
    return $description.Trim()
}

# Get embeddings from Azure OpenAI
function Get-Embeddings {
    param([string[]]$Texts)
    
    $uri = "$Endpoint/openai/deployments/$EmbeddingModel/embeddings?api-version=2024-02-01"
    
    # Get access token using Azure CLI
    $token = az account get-access-token --resource "https://cognitiveservices.azure.com" --query accessToken -o tsv
    if (-not $token) {
        Write-Error "Failed to get Azure access token. Run 'az login' first."
        exit 1
    }
    
    $headers = @{
        "Authorization" = "Bearer $token"
        "Content-Type" = "application/json"
    }
    
    $body = @{
        input = $Texts
    } | ConvertTo-Json
    
    $response = Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $body
    
    # Return as array of arrays (avoid PowerShell flattening)
    $result = @()
    $response.data | Sort-Object index | ForEach-Object { 
        $result += ,($_.embedding)  # Comma operator prevents flattening
    }
    return $result
}

# Calculate cosine similarity
function Get-CosineSimilarity {
    param(
        [double[]]$Vector1,
        [double[]]$Vector2
    )
    
    $dotProduct = 0.0
    $norm1 = 0.0
    $norm2 = 0.0
    
    for ($i = 0; $i -lt $Vector1.Length; $i++) {
        $dotProduct += $Vector1[$i] * $Vector2[$i]
        $norm1 += $Vector1[$i] * $Vector1[$i]
        $norm2 += $Vector2[$i] * $Vector2[$i]
    }
    
    $norm1 = [Math]::Sqrt($norm1)
    $norm2 = [Math]::Sqrt($norm2)
    
    if ($norm1 -eq 0 -or $norm2 -eq 0) { return 0 }
    
    return $dotProduct / ($norm1 * $norm2)
}

# Main execution
Write-Host "`n🧪 Skill Trigger Test: $SkillName" -ForegroundColor Cyan
Write-Host "═" * 60

# Load skill description
$skillDescription = Get-SkillDescription -Path $SkillFile
Write-Host "`n📄 Skill Description:" -ForegroundColor Yellow
Write-Host "   $($skillDescription.Substring(0, [Math]::Min(100, $skillDescription.Length)))..."

# Load test prompts
$prompts = Get-Content $PromptsFile | ConvertFrom-Json
Write-Host "`n📝 Test Prompts:" -ForegroundColor Yellow
Write-Host "   Should trigger: $($prompts.shouldTrigger.Count)"
Write-Host "   Should NOT trigger: $($prompts.shouldNotTrigger.Count)"

# Generate embeddings
Write-Host "`n🔄 Generating embeddings..." -ForegroundColor Yellow
$allTexts = @($skillDescription) + $prompts.shouldTrigger + $prompts.shouldNotTrigger
$embeddings = Get-Embeddings -Texts $allTexts

$skillEmbedding = $embeddings[0]

# Test shouldTrigger prompts
Write-Host "`n✅ Should Trigger Tests:" -ForegroundColor Green
$passedTrigger = 0
$failedTrigger = 0

for ($i = 0; $i -lt $prompts.shouldTrigger.Count; $i++) {
    $prompt = $prompts.shouldTrigger[$i]
    $promptEmbedding = $embeddings[$i + 1]  # +1 because skill is at index 0
    $similarity = Get-CosineSimilarity -Vector1 $skillEmbedding -Vector2 $promptEmbedding
    $passed = $similarity -ge $MinConfidence
    
    if ($passed) {
        Write-Host "   ✅ " -NoNewline -ForegroundColor Green
        $passedTrigger++
    } else {
        Write-Host "   ❌ " -NoNewline -ForegroundColor Red
        $failedTrigger++
    }
    Write-Host "$([Math]::Round($similarity * 100))% " -NoNewline
    Write-Host "- $prompt"
}

# Test shouldNotTrigger prompts
Write-Host "`n🚫 Should NOT Trigger Tests:" -ForegroundColor Magenta
$passedNotTrigger = 0
$failedNotTrigger = 0

$notTriggerStartIndex = 1 + $prompts.shouldTrigger.Count
for ($i = 0; $i -lt $prompts.shouldNotTrigger.Count; $i++) {
    $prompt = $prompts.shouldNotTrigger[$i]
    $promptEmbedding = $embeddings[$notTriggerStartIndex + $i]
    $similarity = Get-CosineSimilarity -Vector1 $skillEmbedding -Vector2 $promptEmbedding
    $passed = $similarity -lt $MinConfidence  # Should NOT match
    
    if ($passed) {
        Write-Host "   ✅ " -NoNewline -ForegroundColor Green
        $passedNotTrigger++
    } else {
        Write-Host "   ⚠️ " -NoNewline -ForegroundColor Yellow
        $failedNotTrigger++
    }
    Write-Host "$([Math]::Round($similarity * 100))% " -NoNewline
    Write-Host "- $prompt"
}

# Summary
Write-Host "`n" + "═" * 60
Write-Host "📊 Results Summary" -ForegroundColor Cyan
Write-Host "   Should Trigger:     $passedTrigger/$($prompts.shouldTrigger.Count) passed"
Write-Host "   Should NOT Trigger: $passedNotTrigger/$($prompts.shouldNotTrigger.Count) passed"

$totalPassed = $passedTrigger + $passedNotTrigger
$totalTests = $prompts.shouldTrigger.Count + $prompts.shouldNotTrigger.Count
$passRate = [Math]::Round(($totalPassed / $totalTests) * 100)

Write-Host "`n   Overall: $totalPassed/$totalTests ($passRate%)" -ForegroundColor $(if ($passRate -ge 80) { "Green" } elseif ($passRate -ge 60) { "Yellow" } else { "Red" })

if ($failedTrigger -gt 0 -or $failedNotTrigger -gt 0) {
    Write-Host "`n⚠️ Some tests failed. Consider:" -ForegroundColor Yellow
    if ($failedTrigger -gt 0) {
        Write-Host "   - Adding more trigger phrases to skill description"
    }
    if ($failedNotTrigger -gt 0) {
        Write-Host "   - Adding 'DO NOT USE FOR:' section to disambiguate"
    }
    exit 1
}

Write-Host "`n✅ All tests passed!" -ForegroundColor Green
exit 0
