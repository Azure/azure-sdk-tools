# Test script for branch naming logic in archetype-typespec-emitter.yml
# This tests the PowerShell logic that creates branch names with emitter disambiguation

param(
    [switch]$Verbose
)

function Test-BranchNaming {
    param(
        [string]$TestName,
        [string]$EmitterPackagePath,
        [string]$BuildReason,
        [string]$SourceBranch,
        [string]$BuildNumber,
        [string]$Expected
    )

    Write-Host "`n🔍 Test: $TestName" -ForegroundColor Cyan
    if ($Verbose) {
        Write-Host "  EmitterPackagePath: '$EmitterPackagePath'"
        Write-Host "  BuildReason: '$BuildReason'"
        Write-Host "  SourceBranch: '$SourceBranch'"
        Write-Host "  BuildNumber: '$BuildNumber'"
    }

    # This is the exact logic from archetype-typespec-emitter.yml
    $sourceBranch = $SourceBranch
    $buildReason = $BuildReason
    $buildNumber = $BuildNumber
    $emitterPackagePath = $EmitterPackagePath

    # Create emitter identifier from package path for disambiguation
    $emitterIdentifier = ""
    if (-not [string]::IsNullOrWhiteSpace($emitterPackagePath)) {
        # Extract filename without extension and make it safe for branch names
        $emitterIdentifier = [System.IO.Path]::GetFileNameWithoutExtension($emitterPackagePath)
        # Replace any characters that aren't alphanumeric, hyphens, or underscores
        $emitterIdentifier = $emitterIdentifier -replace '[^a-zA-Z0-9\-_]', '-'
        # Remove any leading/trailing hyphens and convert to lowercase
        $emitterIdentifier = $emitterIdentifier.Trim('-').ToLower()
        if (-not [string]::IsNullOrWhiteSpace($emitterIdentifier)) {
            $emitterIdentifier = "-$emitterIdentifier"
        }
    }

    if ($buildReason -eq 'Schedule') {
        $branchName = "validate-typespec-scheduled$emitterIdentifier"
    } elseif ($sourceBranch -match "^refs/pull/(\d+)/(head|merge)$") {
        $branchName = "validate-typespec-pr-$($Matches[1])$emitterIdentifier"
    } else {
        $branchName = "validate-typespec-$buildNumber$emitterIdentifier"
    }

    Write-Host "  Generated: '$branchName'"
    Write-Host "  Expected:  '$Expected'"

    if ($branchName -eq $Expected) {
        Write-Host "  ✅ PASS" -ForegroundColor Green
        return $true
    } else {
        Write-Host "  ❌ FAIL" -ForegroundColor Red
        return $false
    }
}

# Test cases
$testCases = @(
    @{
        Name = "Backward compatibility (no emitter path)"
        EmitterPackagePath = ""
        BuildReason = "IndividualCI"
        SourceBranch = "refs/heads/main"
        BuildNumber = "20240101.1"
        Expected = "validate-typespec-20240101.1"
    },
    @{
        Name = "Simple emitter package"
        EmitterPackagePath = "eng/emitter-package.json"
        BuildReason = "IndividualCI"
        SourceBranch = "refs/heads/main"
        BuildNumber = "20240101.2"
        Expected = "validate-typespec-20240101.2-emitter-package"
    },
    @{
        Name = "Java emitter disambiguation"
        EmitterPackagePath = "eng/java-emitter-package.json"
        BuildReason = "IndividualCI"
        SourceBranch = "refs/heads/main"
        BuildNumber = "20240101.3"
        Expected = "validate-typespec-20240101.3-java-emitter-package"
    },
    @{
        Name = "Python emitter disambiguation"
        EmitterPackagePath = "eng/python-emitter-package.json"
        BuildReason = "IndividualCI"
        SourceBranch = "refs/heads/main"
        BuildNumber = "20240101.4"
        Expected = "validate-typespec-20240101.4-python-emitter-package"
    },
    @{
        Name = "PR build with emitter"
        EmitterPackagePath = "eng/dotnet-emitter.json"
        BuildReason = "PullRequest"
        SourceBranch = "refs/pull/1234/head"
        BuildNumber = "20240101.5"
        Expected = "validate-typespec-pr-1234-dotnet-emitter"
    },
    @{
        Name = "Scheduled build with emitter"
        EmitterPackagePath = "eng/typescript-emitter.json"
        BuildReason = "Schedule"
        SourceBranch = "refs/heads/main"
        BuildNumber = "20240101.6"
        Expected = "validate-typespec-scheduled-typescript-emitter"
    },
    @{
        Name = "Special characters handling"
        EmitterPackagePath = "eng/emitters/special@char#emitter.json"
        BuildReason = "IndividualCI"
        SourceBranch = "refs/heads/main"
        BuildNumber = "20240101.7"
        Expected = "validate-typespec-20240101.7-special-char-emitter"
    },
    @{
        Name = "Nested path handling"
        EmitterPackagePath = "tools/emitters/complex/path/nested-emitter-package.json"
        BuildReason = "IndividualCI"
        SourceBranch = "refs/heads/main"
        BuildNumber = "20240101.8"
        Expected = "validate-typespec-20240101.8-nested-emitter-package"
    }
)

Write-Host "🧪 Testing Branch Naming Logic from archetype-typespec-emitter.yml" -ForegroundColor Yellow
Write-Host "=" * 70

$passCount = 0
$totalCount = $testCases.Count

foreach ($testCase in $testCases) {
    $result = Test-BranchNaming @testCase
    if ($result) { $passCount++ }
}

Write-Host "`n📊 Results:" -ForegroundColor Yellow
Write-Host "Passed: $passCount/$totalCount" -ForegroundColor $(if ($passCount -eq $totalCount) { "Green" } else { "Red" })

if ($passCount -eq $totalCount) {
    Write-Host "`n✅ All tests passed! The branch naming logic works correctly." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n❌ Some tests failed. Please review the logic." -ForegroundColor Red
    exit 1
}