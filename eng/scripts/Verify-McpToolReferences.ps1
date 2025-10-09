[CmdletBinding()]
param(
    [string]$InstructionsPath,
    [string]$ToolSourcePath,
    [string]$ReferencePattern = 'azsdk_[A-Za-z0-9_]+'
)

function Get-LevenshteinDistance {
    param(
        [Parameter(Mandatory)] [string]$Source,
        [Parameter(Mandatory)] [string]$Target
    )

    if ($Source -eq $Target) {
        return 0
    }

    if ([string]::IsNullOrEmpty($Source)) {
        return $Target.Length
    }

    if ([string]::IsNullOrEmpty($Target)) {
        return $Source.Length
    }

    $source = $Source.ToLowerInvariant()
    $target = $Target.ToLowerInvariant()

    $sourceLength = $source.Length
    $targetLength = $target.Length

    $previous = [int[]]::new($targetLength + 1)
    $current = [int[]]::new($targetLength + 1)

    for ($j = 0; $j -le $targetLength; $j++) {
        $previous[$j] = $j
    }

    for ($i = 0; $i -lt $sourceLength; $i++) {
        $current[0] = $i + 1
        $sourceChar = $source[$i]

        for ($j = 0; $j -lt $targetLength; $j++) {
            $cost = if ($sourceChar -eq $target[$j]) { 0 } else { 1 }
            $insertion = $current[$j] + 1
            $deletion = $previous[$j + 1] + 1
            $replacement = $previous[$j] + $cost
            $current[$j + 1] = [Math]::Min([Math]::Min($insertion, $deletion), $replacement)
        }

        $temp = $previous
        $previous = $current
        $current = $temp
    }

    return $previous[$targetLength]
}

function Get-BestToolSuggestion {
    param(
        [Parameter(Mandatory)] [string]$MissingTool,
        [Parameter(Mandatory)] [string[]]$DeclaredTools,
        [double]$MinimumScore = 0.7
    )

    $bestCandidate = $null
    $bestScore = 0.0

    foreach ($candidate in $DeclaredTools) {
        $distance = Get-LevenshteinDistance -Source $MissingTool -Target $candidate
        $maxLength = [Math]::Max($MissingTool.Length, $candidate.Length)
        if ($maxLength -eq 0) {
            continue
        }

        $score = 1.0 - ($distance / $maxLength)
        if ($score -gt $bestScore) {
            $bestScore = $score
            $bestCandidate = [PSCustomObject]@{ Name = $candidate; Score = $score }
        }
    }

    if ($bestCandidate -and $bestCandidate.Score -ge $MinimumScore) {
        return $bestCandidate
    }

    return $null
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '../../..')

if (-not (Test-Path -LiteralPath $InstructionsPath)) {
    Write-Error "Instructions path not found: $InstructionsPath"
    exit 1
}

if (-not (Test-Path -LiteralPath $ToolSourcePath)) {
    Write-Error "Tool source path not found: $ToolSourcePath"
    exit 1
}

try {
    $instructionPattern = [regex]::new($ReferencePattern)
}
catch {
    Write-Error "Invalid reference pattern '$ReferencePattern'. $_"
    exit 1
}
$instructionTools = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$instructionReferenceMap = [System.Collections.Generic.Dictionary[string,System.Collections.Generic.List[string]]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($file in Get-ChildItem -Path $InstructionsPath -Recurse -File) {
    $lines = Get-Content -LiteralPath $file.FullName
    if ($lines -is [string]) {
        $lines = @($lines)
    }
    for ($index = 0; $index -lt $lines.Length; $index++) {
        $lineNumber = $index + 1
        $lineMatches = $instructionPattern.Matches($lines[$index])

        foreach ($match in $lineMatches) {
            $toolName = $match.Value
            $instructionTools.Add($toolName) | Out-Null

            if (-not $instructionReferenceMap.ContainsKey($toolName)) {
                $instructionReferenceMap[$toolName] = [System.Collections.Generic.List[string]]::new()
            }

            $relativePath = [System.IO.Path]::GetRelativePath($repoRoot, $file.FullName)
            $location = "${relativePath}:${lineNumber}"
            if (-not $instructionReferenceMap[$toolName].Contains($location)) {
                $null = $instructionReferenceMap[$toolName].Add($location)
            }
        }
    }
}

if ($instructionTools.Count -eq 0) {
    Write-Warning "No MCP tool references found under $InstructionsPath"
    exit 0
}

$declarationString = '\[McpServerTool\(Name\s*=\s*\"(' + $ReferencePattern + ')\"'
try {
    $declarationPattern = [regex]::new($declarationString)
}
catch {
    Write-Error "Invalid declaration pattern '$declarationString'. $_"
    exit 1
}
$declaredTools = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($file in Get-ChildItem -Path $ToolSourcePath -Recurse -Include *.cs -File) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    $matches = $declarationPattern.Matches($content)

    foreach ($match in $matches) {
        $toolName = $match.Groups[1].Value
        $declaredTools.Add($toolName) | Out-Null
    }
}

[string[]]$declaredToolNames = @()
if ($declaredTools.Count -gt 0) {
    $declaredToolNames = New-Object string[] $declaredTools.Count
    $declaredTools.CopyTo($declaredToolNames)
}

$missingTools = @()
foreach ($instructionTool in $instructionTools) {
    if (-not $declaredTools.Contains($instructionTool)) {
        $missingTools += $instructionTool
    }
}

if ($missingTools.Count -gt 0) {
    Write-Host "Missing MCP tool declarations for: $($missingTools -join ', ')" -ForegroundColor Red
    foreach ($toolName in $missingTools) {
        $sources = $instructionReferenceMap[$toolName] -join ', '
        Write-Host "  $toolName referenced from: $sources" -ForegroundColor Yellow
        $suggestion = Get-BestToolSuggestion -MissingTool $toolName -DeclaredTools $declaredToolNames
        if ($suggestion) {
            $percentage = '{0:P0}' -f $suggestion.Score
            Write-Host "    Did you mean '$($suggestion.Name)'? Similarity: $percentage" -ForegroundColor Cyan
        }
    }
    exit 1
}

Write-Host "All MCP tools referenced in instructions are declared." -ForegroundColor Green
