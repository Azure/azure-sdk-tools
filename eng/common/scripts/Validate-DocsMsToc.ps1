<#
.SYNOPSIS
Validate the unified Docs.MS ToC YAML produced by Update-DocsMsToc.ps1.

.DESCRIPTION
Fail-closed structural guardrail. Run immediately after Update-DocsMsToc.ps1
emits the ToC file. Exits non-zero with a single concise reason line per
violation when the ToC is structurally invalid or suspiciously degraded.

The checks intentionally cover two failure classes:

  1. Schema/shape corruption — the 783700739 class of bug, where a breaking
     powershell-yaml behavior change caused `children` items to be serialized
     as mappings instead of scalar strings. The validator inspects the raw
     YAML text (not just the parsed object) because the same module that
     produced the bug cannot be trusted to round-trip-parse its own output.

  2. Structural collapse — every named service node missing and only the
     "Other / Uncategorized Packages" sink remaining, on either moniker.

The validator also supports an optional drift check against the previous
known-good ToC (e.g. an artifact from the last successful publish) — if the
total leaf-node count drops by more than a configurable percentage the
pipeline fails.

.PARAMETER TocPath
[Required] Path to the toc.yml just produced by Update-DocsMsToc.ps1.

.PARAMETER PreviousTocPath
[Optional] Path to the previously published toc.yml for drift comparison.

.PARAMETER MinTopLevelNodes
[Optional, default 2] Minimum number of top-level items required, exclusive
of `Other`. With the default, a TOC that has only `Other` will fail.

.PARAMETER MaxNodeCountDropPercent
[Optional, default 25] Maximum allowed drop (percent) in total leaf-node
count vs the previous ToC. Ignored if PreviousTocPath is not supplied.

.PARAMETER SoftFail
[Optional switch] Emit violations as warnings and exit 0. Use this for a
shadow-mode rollout before flipping to enforce.

.EXAMPLE
./Validate-DocsMsToc.ps1 -TocPath ./docs-ref-toc/toc.yml

.EXAMPLE
./Validate-DocsMsToc.ps1 `
  -TocPath        ./docs-ref-toc/toc.yml `
  -PreviousTocPath $(Pipeline.LastGoodToc) `
  -MaxNodeCountDropPercent 30
#>

[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $TocPath,

  [Parameter(Mandatory = $false)]
  [string] $PreviousTocPath,

  [Parameter(Mandatory = $false)]
  [int] $MinTopLevelNodes = 2,

  [Parameter(Mandatory = $false)]
  [int] $MaxNodeCountDropPercent = 25,

  [switch] $SoftFail
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'

. $PSScriptRoot/common.ps1
. $PSScriptRoot/Helpers/PSModule-Helpers.ps1
Install-ModuleIfNotInstalled "powershell-yaml" "0.4.12" | Import-Module

# ---------------------------------------------------------------------------
# Violation reporting

$violations = New-Object System.Collections.Generic.List[string]
function Add-Violation([string] $code, [string] $message) {
  $violations.Add("[$code] $message") | Out-Null
}

# ---------------------------------------------------------------------------
# Load file

if (-not (Test-Path -LiteralPath $TocPath)) {
  Write-Error "TOC not found at: $TocPath"
  exit 2
}
$rawYaml = Get-Content -LiteralPath $TocPath -Raw
if ([string]::IsNullOrWhiteSpace($rawYaml)) {
  Add-Violation 'TOC001' "ToC file is empty."
}

# ---------------------------------------------------------------------------
# 1. Raw-text shape check (defeats trust-the-broken-serializer problem)
#
# A correct emission of `children` is a YAML block sequence of plain scalars:
#
#   children:
#   - somePackage
#   - someOtherPackage
#
# The 783700739 corruption produced PSObject-wrapped strings, which the
# breaking powershell-yaml emitted as mappings whose first key is `Length`
# (the wrapper property). Reject any occurrence of that shape.

$badChildPattern = '(?ms)^\s*children:\s*\r?\n(?:\s*-\s*(?:Length|Chars|value)\s*:|\s*-\s*!!?map\b)'
$badChildMatch  = [regex]::Match($rawYaml, $badChildPattern)
if ($badChildMatch.Success) {
  Add-Violation 'TOC010' `
    "`children` was serialized as a mapping (probable powershell-yaml regression). First match at offset $($badChildMatch.Index)."
}

# Also reject any `children:` entry whose first item is *not* a plain scalar.
# We use a conservative scan: every `children:` block followed by a sequence
# item is flagged if the item is a mapping.
# YAML permits list items at the SAME indentation as their parent key, so the
# leading indent of `-` may equal `children:` (not strictly greater).
$childBlockPattern = '(?m)^( *)children:\s*\r?\n((?:\1 *-[^\r\n]*\r?\n)+)'
foreach ($m in [regex]::Matches($rawYaml, $childBlockPattern)) {
  $block = $m.Groups[2].Value
  foreach ($line in $block -split "`r?`n") {
    if ($line -match '^\s*-\s*\{' -or $line -match '^\s*-\s*\S+\s*:\s*\S') {
      Add-Violation 'TOC011' "`children` item is not a scalar string: '$($line.Trim())'"
      break
    }
  }
}

# ---------------------------------------------------------------------------
# 2. Parse & structural checks

$parsed = $null
try {
  $parsed = ConvertFrom-Yaml $rawYaml
} catch {
  Add-Violation 'TOC002' "ToC could not be parsed as YAML: $($_.Exception.Message)"
}

function Get-ChildItems($node) {
  if ($null -eq $node) { return @() }
  if ($node -is [System.Collections.IDictionary] -and $node.Contains('items')) {
    return @($node['items'])
  }
  return @()
}

function Get-ChildrenStrings($node) {
  if ($null -eq $node) { return @() }
  if ($node -is [System.Collections.IDictionary] -and $node.Contains('children')) {
    return @($node['children'])
  }
  return @()
}

function Count-Leaves($node) {
  if ($null -eq $node) { return 0 }
  # @(...) guards against PowerShell collapsing empty function output to $null,
  # which would trip Set-StrictMode on the .Count access.
  $items    = @(Get-ChildItems $node)
  $children = @(Get-ChildrenStrings $node)
  if ($items.Count -eq 0 -and $children.Count -eq 0) { return 1 }
  $sum = 0
  foreach ($it in $items) { $sum += Count-Leaves $it }
  $sum += $children.Count
  return $sum
}

$rootItems = @()
if ($parsed -is [System.Collections.IList] -and $parsed.Count -ge 1) {
  $root = $parsed[0]
  if ($root -is [System.Collections.IDictionary]) {
    $rootItems = Get-ChildItems $root
    $rootName  = if ($root.Contains('name')) { $root['name'] } else { '' }
    if ($rootName -ne 'Reference') {
      Add-Violation 'TOC020' "Root node name is '$rootName'; expected 'Reference'."
    }
  } else {
    Add-Violation 'TOC021' "Root is not a mapping."
  }
} else {
  Add-Violation 'TOC022' "ToC is not a non-empty sequence."
}

# ---------------------------------------------------------------------------
# 3. Service-node invariants

$serviceNames = @()
foreach ($svc in $rootItems) {
  if ($svc -is [System.Collections.IDictionary] -and $svc.Contains('name')) {
    $name = [string]$svc['name']
    if ([string]::IsNullOrWhiteSpace($name)) {
      Add-Violation 'TOC030' "Service node with empty name."
    } else {
      $serviceNames += $name
    }
  } else {
    Add-Violation 'TOC031' "Service-level entry is not a named mapping."
  }
}

$namedExcludingOther = @($serviceNames | Where-Object { $_ -ne 'Other' })
if ($namedExcludingOther.Count -lt $MinTopLevelNodes) {
  Add-Violation 'TOC040' `
    "Only $($namedExcludingOther.Count) non-Other top-level service node(s); minimum is $MinTopLevelNodes. This is the 783700739 symptom."
}

if ($serviceNames.Count -eq 1 -and $serviceNames[0] -eq 'Other') {
  Add-Violation 'TOC041' "Only 'Other' remains at the top level (no service categories)."
}

# ---------------------------------------------------------------------------
# 4. Drift vs previous good build (optional)

if ($PreviousTocPath) {
  if (-not (Test-Path -LiteralPath $PreviousTocPath)) {
    Write-Warning "PreviousTocPath '$PreviousTocPath' not found; skipping drift check."
  } else {
    $prevYaml   = Get-Content -LiteralPath $PreviousTocPath -Raw
    $prevParsed = $null
    try { $prevParsed = ConvertFrom-Yaml $prevYaml } catch { }
    if ($prevParsed -isnot [System.Collections.IList] -or $prevParsed.Count -lt 1) {
      Write-Warning "Previous TOC at '$PreviousTocPath' could not be parsed; skipping drift check."
    } else {
      $prevLeaves = Count-Leaves $prevParsed[0]
      $curLeaves  = if ($parsed -is [System.Collections.IList] -and $parsed.Count -ge 1) {
                      Count-Leaves $parsed[0]
                    } else { 0 }
      if ($prevLeaves -gt 0) {
        $dropPercent = [math]::Round((($prevLeaves - $curLeaves) / $prevLeaves) * 100, 1)
        Write-Host "Leaf count: previous=$prevLeaves current=$curLeaves drop=${dropPercent}%"
        if ($dropPercent -gt $MaxNodeCountDropPercent) {
          Add-Violation 'TOC050' `
            "Leaf-node count dropped ${dropPercent}% (>${MaxNodeCountDropPercent}%) versus previous good ToC ($prevLeaves -> $curLeaves)."
        }
      }
    }
  }
}

# ---------------------------------------------------------------------------
# Report

if ($violations.Count -eq 0) {
  Write-Host "Validate-DocsMsToc: OK ($($rootItems.Count) top-level nodes; non-Other=$($namedExcludingOther.Count))"
  exit 0
}

Write-Host "Validate-DocsMsToc: $($violations.Count) violation(s):"
foreach ($v in $violations) { Write-Host "  $v" }

if ($SoftFail) {
  Write-Warning "Validate-DocsMsToc: -SoftFail set; reporting only, exit 0."
  exit 0
}

Write-Error "Validate-DocsMsToc: ToC failed validation. Refusing to publish."
exit 1
