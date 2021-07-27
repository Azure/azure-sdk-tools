param(
    [string]$searchDirectory = '.',
    [hashtable]$filters = @{}
)

class StressTestPackageInfo {
    [string]$Namespace
    [string]$Directory
    [string]$ReleaseName
}

function findStressPackages([string]$directory, [hashtable]$filters = @{}) {
    # Bare minimum filter for stress tests
    $filters['stressTest'] = 'true'

    Get-ChildItem -Recurse -Filter 'Chart.yaml' -PipelineVariable chartFile $directory 
      | % { parseChart $chartFile }
      | ? { matchesAnnotations $_ $filters }
      | % { NewStressTestPackageInfo $_ $chartFile }
}

function parseChart([string]$chartFile) {
    ConvertFrom-Yaml (Get-Content -Raw $chartFile)
}

function matchesAnnotations([hashtable]$chart, [hashtable]$filters) {
    foreach ($filter in $filters.GetEnumerator()) {
        if (!$chart.annotations -or $chart.annotations[$filter.Key] -ne $filter.Value) {
            return $false
        }
    }

    return $true
}

function NewStressTestPackageInfo([hashtable]$chart, [System.IO.FileInfo]$chartFile) {
    [StressTestPackageInfo]@{
        Namespace = $chart.annotations.namespace
        Directory = $chartFile.DirectoryName
        ReleaseName = $chart.name
    }
}

# Don't call functions when the script is being dot sourced
if ($MyInvocation.InvocationName -ne ".") {
    findStressPackages $searchDirectory $filters
}
