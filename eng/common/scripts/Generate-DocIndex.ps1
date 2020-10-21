# Generates an index page for cataloging different versions of the Docs
[CmdletBinding()]
Param (
    $DocFx,
    $RepoRoot,
    $DocGenDir
)
. (Join-Path $PSScriptRoot common.ps1)

# The sequence of Bom bytes differs by different encoding. 
# The helper function here is only to strip the utf-8 encoding system as it is used by blob storage list api.
# Return the original string if not in BOM utf-8 sequence.
function removeBomFromString([string]$bomAwareString) {
    if ($bomAwareString.length -le 3) {
        return $bomAwareString
    }
    $bomPatternByteArray = [byte[]] (0xef, 0xbb, 0xbf)
    # The default encoding for powershell is ISO-8859-1, so converting bytes with the encoding.
    $bomAwareBytes = [Text.Encoding]::GetEncoding(28591).GetBytes($bomAwareString.Substring(0, 3))
    if (@(Compare-Object $bomPatternByteArray $bomAwareBytes -SyncWindow 0).Length -eq 0) {
        return $bomAwareString.Substring(3)
    }
    return $bomAwareString
}

function Get-PkgMetadata([Object[]]$metadata, [String]$artifactName, [Hashtable]$filterOptions) {
    $filteredPkg = $metadata | ? { $_.Package -eq $artifactName }
    if (!$filteredPkg) {
        return
    }
    if ($filteredPkg.length -eq 1) {
        return $filteredPkg[0]
    }
    foreach ($filter in $filterOptions.getEnumerator()) {
        $filterKey = $filter.Key.Trim()
        $filterValue = $filter.Value.Trim()
        $filteredPkg = $filteredPkg | ? { $_.$filterKey -eq $filterValue }
    }
    if ($filteredPkg.length -ne 1) {
        LogWarning "We fetch out $($filteredPkg.length) artifact(s) for the same package name $artifactName. "
        LogWarning "Please check csv of Azure/azure-sdk/_data/release/latest repo if this is intended. "
        return 
    }
    return $filteredPkg[0]
}


function Get-CSVMetadata ([string]$MetadataUri) {
    $metadataResponse = Invoke-RestMethod -Uri $MetadataUri -method "GET" -MaximumRetryCount 3 -RetryIntervalSec 10 | ConvertFrom-Csv
    return $metadataResponse
}

function Generate-DocIndex { 
    Param (
        [Parameter(Mandatory = $true)]  [Object[]]  $metadata,
        [Parameter(Mandatory = $true)]  [Hashtable] $filterOptions,
        [Parameter(Mandatory = $true)]  [String]    $blobStorageUrl,
        [Parameter(Mandatory = $true)]  [String]    $blobDirectoryRegex,
        [Parameter(Mandatory = $true)]  [String]    $titleRegex,
        [Parameter(Mandatory = $false)] [String]    $packageNameRegex = '',
        [Parameter(Mandatory = $false)] [String]    $packageNameReplacement = ''
    )
    $pageToken = ""
    # Used for sorting the toc display order
    $orderServiceMapping = @{}
    # This is a pagnation call as storage only return 5000 results as maximum.
    Do {
        $resp = ""
        if (!$pageToken) {
            # First page call.
            $resp = Invoke-RestMethod -Method Get -Uri $blobStorageUrl
        }
        else {
            # Next page call
            $blobStorageUrlPageToken = $blobStorageUrl + "&marker=$pageToken"
            $resp = Invoke-RestMethod -Method Get -Uri $blobStorageUrlPageToken
        }
        # Convert to xml documents. 
        $xmlDoc = [xml](removeBomFromString $resp)
        foreach ($elem in $xmlDoc.EnumerationResults.Blobs.BlobPrefix) {
            # What service return like "dotnet/Azure.AI.Anomalydetector/", needs to fetch out "Azure.AI.Anomalydetector"
            $artifact = $elem.Name -replace $blobDirectoryRegex, '$1'
            # Some languages need to convert the artifact name, e.g azure-data-appconfiguration -> @azure/data-appconfiguration
            if ($packageNameRegex) {
                $artifact = $artifact -replace $packageNameRegex, $packageNameReplacement
            }
            $packageInfo = Get-PkgMetadata -metadata $metadata -artifactName $artifact -filterOption $filterOptions
            $serviceName = ""
            if (!$packageInfo) {
                LogWarning "Did not find the artifacts $artifact from release csv. Please check and update."
                continue
            }
            elseif (!$packageInfo.ServiceName) {
                LogWarning "There is no service name for artifact $artifact. Please check csv of Azure/azure-sdk/_data/release/latest repo if this is intended. "
                # If no service name retrieved, print out warning message, and put it into Other page.
                $serviceName = "Other"
            }
            else {
                $serviceName = $packageInfo.ServiceName.Trim()
            }
            
            $orderServiceMapping[$artifact] = $serviceName
        }
        # Fetch page token
        $pageToken = $xmlDoc.EnumerationResults.NextMarker
    } while ($pageToken)
    return $orderServiceMapping                   
}

function generateDocfxTocContent([Hashtable]$tocContent) {
    LogDebug "Name Reccuring paths with variable names"
    $DocOutDir = "${RepoRoot}/docfx_project"

    LogDebug "Initializing Default DocFx Site..."
    & $($DocFx) init -q -o "${DocOutDir}"
    # The line below is used for testing in local
    #docfx init -q -o "${DocOutDir}"
    LogDebug "Copying template and configuration..."
    New-Item -Path "${DocOutDir}" -Name "templates" -ItemType "directory" -Force
    Copy-Item "${DocGenDir}/templates/*" -Destination "${DocOutDir}/templates" -Force -Recurse
    Copy-Item "${DocGenDir}/docfx.json" -Destination "${DocOutDir}/" -Force
    $YmlPath = "${DocOutDir}/api"
    New-Item -Path $YmlPath -Name "toc.yml" -Force
    $visitedService = @{}
    # Sort and display toc service name by alphabetical order.
    foreach ($serviceMapping in $tocContent.getEnumerator() | Sort Value) {
        $artifact = $serviceMapping.Key
        $serviceName = $serviceMapping.Value
        $fileName = ($serviceName -replace '\s', '').ToLower().Trim()
        if ($visitedService.ContainsKey($serviceName)) {
            Add-Content -Path "$($YmlPath)/${fileName}.md" -Value "#### $artifact"
        }
        else {
            Add-Content -Path "$($YmlPath)/toc.yml" -Value "- name: ${serviceName}`r`n  href: ${fileName}.md"
            New-Item -Path $YmlPath -Name "${fileName}.md" -Force
            Add-Content -Path "$($YmlPath)/${fileName}.md" -Value "#### $artifact"
            $visitedService[$serviceName] = $true
        }
    }

    LogDebug "Creating Site Title and Navigation..."
    New-Item -Path "${DocOutDir}" -Name "toc.yml" -Force
    Add-Content -Path "${DocOutDir}/toc.yml" -Value "- name: Azure SDK for $titleRegex APIs`r`n  href: api/`r`n  homepage: api/index.md"

    LogDebug "Copying root markdowns"
    Copy-Item "$($RepoRoot)/README.md" -Destination "${DocOutDir}/api/index.md" -Force
    Copy-Item "$($RepoRoot)/CONTRIBUTING.md" -Destination "${DocOutDir}/api/CONTRIBUTING.md" -Force

    LogDebug "Building site..."
    & $($DocFx) build "${DocOutDir}/docfx.json"
    # The line below is used for testing in local
    #docfx build "${DocOutDir}/docfx.json"
    Copy-Item "${DocGenDir}/assets/logo.svg" -Destination "${DocOutDir}/_site/" -Force    
}

LogDebug "Reading artifact from storage blob ..."
$tocContent = &$GenerateDocIndexFn
LogDebug "Start generating the docfx toc and build docfx site..."
generateDocfxTocContent $tocContent 
