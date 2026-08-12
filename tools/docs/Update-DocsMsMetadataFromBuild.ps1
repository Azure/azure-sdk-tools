<#
.SYNOPSIS
Manually reproduces the Docs.MS metadata update step for a failed Azure DevOps build.

.DESCRIPTION
Creates an isolated temporary workspace, clones the SDK and docs repositories needed
for the build, downloads requested PackageInfo json files from the build artifacts,
installs any language-specific validation prerequisites into the workspace, and then
runs the cloned repo's eng/common/scripts/Update-DocsMsMetadata.ps1 script.

.PARAMETER PackageNames
One or more package names to update. For JavaScript, either the scoped package name
(for example, @azure/storage-blob) or the artifact-style name (azure-storage-blob)
may be used.

.PARAMETER BuildUrl
Azure DevOps build results URL, for example:
https://dev.azure.com/azure-sdk/internal/_build/results?buildId=6307334&view=results
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]] $PackageNames,

    [Parameter(Mandatory = $true)]
    [string] $BuildUrl
)

Set-StrictMode -Version 3
$ErrorActionPreference = 'Stop'
$PSNativeCommandArgumentPassing = 'Legacy'

$repoRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($PSScriptRoot, "..", ".."))
. ([System.IO.Path]::Combine($repoRoot, "eng", "common", "scripts", "logging.ps1"))

$script:AdoResourceId = "499b84ac-1321-427f-aa17-267ca6975798"
$script:AdoApiVersion = "7.1"

function Throw-ScriptFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Message,

        [Parameter(Mandatory = $true)]
        [int] $ExitCode
    )

    $exception = [System.InvalidOperationException]::new($Message)
    $exception.Data["ExitCode"] = $ExitCode
    throw $exception
}

function Format-NativeArgumentForDisplay {
    param(
        [AllowEmptyString()]
        [string] $Argument
    )

    if ($null -eq $Argument) {
        return "''"
    }

    if ($Argument.Length -eq 0) {
        return "''"
    }

    if ($Argument -match "[\s'`"]") {
        return "'$($Argument.Replace("'", "''"))'"
    }

    return $Argument
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [string[]] $Arguments = @(),

        [string] $ExecutePath,

        [Parameter(Mandatory = $true)]
        [string] $FailureMessage,

        [Parameter(Mandatory = $true)]
        [int] $ScriptExitCode,

        [switch] $DoNotThrow
    )

    $displayCommand = @((Format-NativeArgumentForDisplay $FilePath))
    $displayCommand += @($Arguments | ForEach-Object { Format-NativeArgumentForDisplay "$_" })

    $exitCode = 0
    $caughtException = $null

    LogGroupStart ($displayCommand -join " ")
    if ($ExecutePath) {
        Push-Location $ExecutePath
    }

    try {
        & $FilePath @Arguments
        $exitCode = $LASTEXITCODE
    }
    catch {
        $caughtException = $_.Exception
        $exitCode = 1
    }
    finally {
        if ($ExecutePath) {
            Pop-Location
        }
        LogGroupEnd
    }

    if ($caughtException) {
        if ($DoNotThrow) {
            return $exitCode
        }

        Throw-ScriptFailure "$FailureMessage $($caughtException.Message)" $ScriptExitCode
    }

    if ($exitCode -and -not $DoNotThrow) {
        Throw-ScriptFailure "$FailureMessage Exit code: $exitCode." $ScriptExitCode
    }

    return $exitCode
}

function ConvertTo-RepoRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $normalizedPath = $Path.Trim()
    $normalizedPath = $normalizedPath.Replace("\", "/")
    $normalizedPath = $normalizedPath.TrimStart("/")

    while ($normalizedPath.StartsWith("./")) {
        $normalizedPath = $normalizedPath.Substring(2)
    }

    return $normalizedPath
}

function Get-OptionalPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        $InputObject,

        [Parameter(Mandatory = $true)]
        [string] $PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Escape-UriPathSegment {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Segment
    )

    return [System.Uri]::EscapeDataString($Segment)
}

function ConvertTo-QueryString {
    param(
        [hashtable] $Query = @{}
    )

    if (!$Query -or $Query.Count -eq 0) {
        return ""
    }

    $queryParts = foreach ($entry in $Query.GetEnumerator()) {
        "{0}={1}" -f [System.Uri]::EscapeDataString($entry.Key), [System.Uri]::EscapeDataString("$($entry.Value)")
    }

    return ($queryParts -join "&")
}

function Normalize-PackageNames {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $PackageNames
    )

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $normalizedNames = [System.Collections.Generic.List[string]]::new()

    foreach ($packageName in $PackageNames) {
        if ($null -eq $packageName) {
            continue
        }

        $trimmedName = $packageName.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedName)) {
            continue
        }

        if ($seen.Add($trimmedName)) {
            $normalizedNames.Add($trimmedName)
        }
    }

    if ($normalizedNames.Count -eq 0) {
        Throw-ScriptFailure "At least one non-empty package name must be provided." 1
    }

    return @($normalizedNames)
}

function Get-BuildContextFromUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BuildUrl
    )

    try {
        $uri = [System.Uri] $BuildUrl
    }
    catch {
        Throw-ScriptFailure "BuildUrl '$BuildUrl' is not a valid URI." 1
    }

    if ($uri.Host -ne "dev.azure.com") {
        Throw-ScriptFailure "BuildUrl '$BuildUrl' must target dev.azure.com." 1
    }

    $pathSegments = $uri.AbsolutePath.Trim("/") -split "/"
    if ($pathSegments.Count -lt 4 -or $pathSegments[2] -ne "_build" -or $pathSegments[3] -ne "results") {
        Throw-ScriptFailure "BuildUrl '$BuildUrl' must be an Azure DevOps build results URL." 1
    }

    $queryParameters = @{}
    foreach ($pair in $uri.Query.TrimStart("?") -split "&") {
        if ([string]::IsNullOrWhiteSpace($pair)) {
            continue
        }

        $keyValue = $pair -split "=", 2
        $key = [System.Uri]::UnescapeDataString($keyValue[0])
        $value = ""
        if ($keyValue.Count -gt 1) {
            $value = [System.Uri]::UnescapeDataString($keyValue[1])
        }
        $queryParameters[$key] = $value
    }

    $buildId = 0
    if (-not $queryParameters.ContainsKey("buildId") -or -not [int]::TryParse($queryParameters["buildId"], [ref] $buildId)) {
        Throw-ScriptFailure "BuildUrl '$BuildUrl' does not contain a valid buildId query parameter." 1
    }

    return [PSCustomObject]@{
        Organization = [System.Uri]::UnescapeDataString($pathSegments[0])
        Project      = [System.Uri]::UnescapeDataString($pathSegments[1])
        BuildId      = $buildId
    }
}

function Get-JavascriptPublicDevOpsRegistry {
    return "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-js/npm/registry/"
}

function Get-PythonPublicPackageIndex {
    return "https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-python/pypi/simple/"
}

function Get-SupportedRepositoryConfigurations {
    return @{
        "Azure/azure-sdk-for-net" = [PSCustomObject]@{
            Ecosystem             = "net"
            Language              = "dotnet"
            SdkRepository         = "Azure/azure-sdk-for-net"
            DocsRepository        = "MicrosoftDocs/azure-docs-sdk-dotnet"
            DocsSparsePaths       = @("api/overview/azure", "metadata")
            PackageSourceOverride = $null
        }
        "Azure/azure-sdk-for-java" = [PSCustomObject]@{
            Ecosystem             = "java"
            Language              = "java"
            SdkRepository         = "Azure/azure-sdk-for-java"
            DocsRepository        = "MicrosoftDocs/azure-docs-sdk-java"
            DocsSparsePaths       = @("docs-ref-services", "metadata")
            PackageSourceOverride = $null
        }
        "Azure/azure-sdk-for-js" = [PSCustomObject]@{
            Ecosystem             = "js"
            Language              = "javascript"
            SdkRepository         = "Azure/azure-sdk-for-js"
            DocsRepository        = "MicrosoftDocs/azure-docs-sdk-node"
            DocsSparsePaths       = @("docs-ref-services", "metadata", "ci-configs")
            PackageSourceOverride = Get-JavascriptPublicDevOpsRegistry
        }
        "Azure/azure-sdk-for-python" = [PSCustomObject]@{
            Ecosystem             = "python"
            Language              = "python"
            SdkRepository         = "Azure/azure-sdk-for-python"
            DocsRepository        = "MicrosoftDocs/azure-docs-sdk-python"
            DocsSparsePaths       = @("docs-ref-services", "metadata")
            PackageSourceOverride = Get-PythonPublicPackageIndex
        }
        "Azure/azure-sdk-for-cpp" = [PSCustomObject]@{
            Ecosystem             = "cpp"
            Language              = "cpp"
            SdkRepository         = "Azure/azure-sdk-for-cpp"
            DocsRepository        = "MicrosoftDocs/azure-docs-sdk-cpp"
            DocsSparsePaths       = @("docs-ref-services", "metadata", "ci-configs")
            PackageSourceOverride = $null
        }
    }
}

function Get-GitHubRepositoryIdFromUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryUrl
    )

    try {
        $uri = [System.Uri] $RepositoryUrl
    }
    catch {
        return $null
    }

    if ($uri.Host -ne "github.com") {
        return $null
    }

    $segments = $uri.AbsolutePath.Trim("/") -split "/"
    if ($segments.Count -lt 2) {
        return $null
    }

    return "{0}/{1}" -f $segments[0], $segments[1]
}

function Get-BuildRepositoryId {
    param(
        [Parameter(Mandatory = $true)]
        $Build
    )

    $candidates = @()

    $repository = Get-OptionalPropertyValue -InputObject $Build -PropertyName "repository"
    if ($null -ne $repository) {
        $repositoryName = Get-OptionalPropertyValue -InputObject $repository -PropertyName "name"
        if (-not [string]::IsNullOrWhiteSpace("$repositoryName")) {
            $candidates += "$repositoryName"
        }

        $repositoryId = Get-OptionalPropertyValue -InputObject $repository -PropertyName "id"
        if (-not [string]::IsNullOrWhiteSpace("$repositoryId")) {
            $candidates += "$repositoryId"
        }

        $repositoryUrl = Get-OptionalPropertyValue -InputObject $repository -PropertyName "url"
        if (-not [string]::IsNullOrWhiteSpace("$repositoryUrl")) {
            $repositoryIdFromUrl = Get-GitHubRepositoryIdFromUrl -RepositoryUrl "$repositoryUrl"
            if ($repositoryIdFromUrl) {
                $candidates += $repositoryIdFromUrl
            }
        }
    }

    foreach ($candidate in $candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            return $candidate.Trim()
        }
    }

    Throw-ScriptFailure "The build metadata did not include a supported repository identifier." 2
}

function Get-EcosystemConfigurationFromBuildRepo {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BuildRepositoryId
    )

    $supportedRepositories = Get-SupportedRepositoryConfigurations
    if ($supportedRepositories.ContainsKey($BuildRepositoryId)) {
        return $supportedRepositories[$BuildRepositoryId]
    }

    Throw-ScriptFailure "Build repository '$BuildRepositoryId' is not one of the supported Azure SDK language repos." 2
}

function Get-AdoBearerToken {
    $azPath = Get-Command az -ErrorAction SilentlyContinue | Select-Object -First 1
    if (!$azPath) {
        Throw-ScriptFailure "Azure CLI was not found. Install 'az' and sign in before running this script." 2
    }

    $commandDisplay = "az account get-access-token --resource $script:AdoResourceId --query accessToken --output tsv"
    LogGroupStart $commandDisplay
    try {
        $token = & $azPath.Source account get-access-token --resource $script:AdoResourceId --query accessToken --output tsv
        $exitCode = $LASTEXITCODE
    }
    finally {
        LogGroupEnd
    }

    if ($exitCode) {
        Throw-ScriptFailure "Failed to get an Azure DevOps token from Azure CLI. Ensure 'az' is signed in and can access Azure DevOps." 2
    }

    $trimmedToken = "$token".Trim()
    if ([string]::IsNullOrWhiteSpace($trimmedToken)) {
        Throw-ScriptFailure "Azure CLI returned an empty Azure DevOps access token." 2
    }

    return $trimmedToken
}

function Invoke-AdoGet {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Organization,

        [Parameter(Mandatory = $true)]
        [string] $Project,

        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [hashtable] $Headers,

        [hashtable] $Query = @{}
    )

    $queryString = ConvertTo-QueryString -Query $Query
    $uri = "https://dev.azure.com/{0}/{1}/_apis{2}?api-version={3}" -f `
        (Escape-UriPathSegment $Organization), `
        (Escape-UriPathSegment $Project), `
        $Path, `
        $script:AdoApiVersion

    if ($queryString) {
        $uri = "$uri&$queryString"
    }

    try {
        return Invoke-RestMethod -Method Get -Uri $uri -Headers $Headers
    }
    catch {
        Throw-ScriptFailure "Azure DevOps GET request failed for '$uri'. $($_.Exception.Message)" 2
    }
}

function Get-PackageInfoArtifactBaseName {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [string] $PackageName
    )

    if ($RepositoryConfiguration.Ecosystem -eq "js") {
        return $PackageName.Trim().TrimStart("@").Replace("/", "-")
    }

    return $PackageName.Trim()
}

function Get-PackageInfoArtifactFileName {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [string] $PackageName
    )

    return "{0}.json" -f (Get-PackageInfoArtifactBaseName -RepositoryConfiguration $RepositoryConfiguration -PackageName $PackageName)
}

function Get-RequestedPackageContexts {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [string[]] $PackageNames
    )

    $seenArtifactNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $packageContexts = [System.Collections.Generic.List[object]]::new()

    foreach ($packageName in $PackageNames) {
        $artifactBaseName = Get-PackageInfoArtifactBaseName -RepositoryConfiguration $RepositoryConfiguration -PackageName $packageName
        if ($seenArtifactNames.Add($artifactBaseName)) {
            $packageContexts.Add([PSCustomObject]@{
                RequestedName    = $packageName
                ArtifactBaseName = $artifactBaseName
                ArtifactFileName = "{0}.json" -f $artifactBaseName
            })
        }
        else {
            LogWarning "Skipping duplicate package request '$packageName' because it maps to the same PackageInfo artifact as an earlier input."
        }
    }

    return @($packageContexts)
}

function Get-ArtifactSubPathDownloadUrl {
    param(
        [Parameter(Mandatory = $true)]
        [string] $DownloadUrl,

        [Parameter(Mandatory = $true)]
        [string] $SubPath
    )

    $normalizedSubPath = $SubPath
    if (!$normalizedSubPath.StartsWith("/")) {
        $normalizedSubPath = "/$normalizedSubPath"
    }

    $zipFormatPattern = '(%24format|\$format|format)=zip'
    if ($DownloadUrl -match $zipFormatPattern) {
        return ($DownloadUrl -replace $zipFormatPattern, ('$1=file&subPath={0}' -f $normalizedSubPath))
    }

    if ($DownloadUrl.Contains("?")) {
        return "$DownloadUrl&subPath=$normalizedSubPath"
    }

    return "$DownloadUrl?subPath=$normalizedSubPath"
}

function Test-PackageInfoMatchesRequestedPackage {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [string] $RequestedPackageName,

        [Parameter(Mandatory = $true)]
        $PackageInfo
    )

    $expectedArtifactBaseName = Get-PackageInfoArtifactBaseName -RepositoryConfiguration $RepositoryConfiguration -PackageName $RequestedPackageName

    if ($PackageInfo.PSObject.Properties["ArtifactName"] -and "$($PackageInfo.ArtifactName)".Trim()) {
        return $PackageInfo.ArtifactName -ieq $expectedArtifactBaseName
    }

    return $PackageInfo.Name -ieq $RequestedPackageName
}

function Download-PackageInfoJson {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [PSCustomObject] $PackageContext,

        [Parameter(Mandatory = $true)]
        [array] $Artifacts,

        [Parameter(Mandatory = $true)]
        [hashtable] $Headers,

        [Parameter(Mandatory = $true)]
        [string] $PackageInfoDirectory
    )

    $artifactCandidates = @($Artifacts | Sort-Object @{ Expression = { if ($_.name -eq "packages") { 0 } else { 1 } } }, name)
    $destinationPath = [System.IO.Path]::Combine($PackageInfoDirectory, $PackageContext.ArtifactFileName)

    foreach ($artifact in $artifactCandidates) {
        $artifactResource = Get-OptionalPropertyValue -InputObject $artifact -PropertyName "resource"
        $downloadUrl = Get-OptionalPropertyValue -InputObject $artifactResource -PropertyName "downloadUrl"
        if ([string]::IsNullOrWhiteSpace("$downloadUrl")) {
            continue
        }

        $fileDownloadUrl = Get-ArtifactSubPathDownloadUrl `
            -DownloadUrl "$downloadUrl" `
            -SubPath "PackageInfo/$($PackageContext.ArtifactFileName)"

        try {
            Invoke-WebRequest -Uri $fileDownloadUrl -Headers $Headers -OutFile $destinationPath | Out-Null
        }
        catch {
            if (Test-Path $destinationPath) {
                Remove-Item -Path $destinationPath -Force
            }
            continue
        }

        try {
            $packageInfo = Get-Content -Path $destinationPath -Raw | ConvertFrom-Json
        }
        catch {
            Throw-ScriptFailure "Downloaded package info '$destinationPath' is not valid JSON." 3
        }

        if (-not (Test-PackageInfoMatchesRequestedPackage `
                -RepositoryConfiguration $RepositoryConfiguration `
                -RequestedPackageName $PackageContext.RequestedName `
                -PackageInfo $packageInfo)) {
            Throw-ScriptFailure "Downloaded package info '$destinationPath' did not match the requested package '$($PackageContext.RequestedName)'." 3
        }

        $serviceDirectory = Get-OptionalPropertyValue -InputObject $packageInfo -PropertyName "ServiceDirectory"
        $directoryPath = Get-OptionalPropertyValue -InputObject $packageInfo -PropertyName "DirectoryPath"
        $readMePath = Get-OptionalPropertyValue -InputObject $packageInfo -PropertyName "ReadMePath"
        if (-not ($serviceDirectory -or $directoryPath -or $readMePath)) {
            Throw-ScriptFailure "Downloaded package info '$destinationPath' did not contain enough path data to compute the SDK sparse checkout." 3
        }

        return [PSCustomObject]@{
            RequestedName = $PackageContext.RequestedName
            ArtifactName  = $PackageContext.ArtifactBaseName
            ArtifactFile  = $PackageContext.ArtifactFileName
            Artifact      = Get-OptionalPropertyValue -InputObject $artifact -PropertyName "name"
            Path          = $destinationPath
            PackageInfo   = $packageInfo
        }
    }

    $availableArtifacts = @($artifactCandidates | ForEach-Object { Get-OptionalPropertyValue -InputObject $_ -PropertyName "name" }) -join ", "
    Throw-ScriptFailure "Could not find PackageInfo/$($PackageContext.ArtifactFileName) in the build artifacts. Available artifacts: $availableArtifacts" 3
}

function Get-SdkSparseCheckoutPaths {
    param(
        [Parameter(Mandatory = $true)]
        [array] $DownloadedPackageInfos
    )

    $seenPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $sparsePaths = [System.Collections.Generic.List[string]]::new()

    foreach ($fixedPath in @("eng", ".github")) {
        if ($seenPaths.Add($fixedPath)) {
            $sparsePaths.Add($fixedPath)
        }
    }

    foreach ($downloadedPackage in $DownloadedPackageInfos) {
        $packageInfo = $downloadedPackage.PackageInfo
        $resolvedPath = $null

        $serviceDirectory = Get-OptionalPropertyValue -InputObject $packageInfo -PropertyName "ServiceDirectory"
        $directoryPath = Get-OptionalPropertyValue -InputObject $packageInfo -PropertyName "DirectoryPath"
        $readMePath = Get-OptionalPropertyValue -InputObject $packageInfo -PropertyName "ReadMePath"

        if ($serviceDirectory) {
            $resolvedPath = ConvertTo-RepoRelativePath -Path ([System.IO.Path]::Combine("sdk", "$serviceDirectory"))
        }
        elseif ($directoryPath) {
            $resolvedPath = ConvertTo-RepoRelativePath -Path "$directoryPath"
        }
        elseif ($readMePath) {
            $resolvedPath = ConvertTo-RepoRelativePath -Path (Split-Path -Path "$readMePath" -Parent)
        }

        if ([string]::IsNullOrWhiteSpace($resolvedPath)) {
            Throw-ScriptFailure "Package '$($downloadedPackage.RequestedName)' did not have a ServiceDirectory, DirectoryPath, or ReadMePath that could be used for sparse checkout." 3
        }

        if ($seenPaths.Add($resolvedPath)) {
            $sparsePaths.Add($resolvedPath)
        }
    }

    return @($sparsePaths)
}

function New-WorkspaceLayout {
    param(
        [Parameter(Mandatory = $true)]
        [int] $BuildId
    )

    $workspaceRoot = [System.IO.Path]::Combine(
        [System.IO.Path]::GetTempPath(),
        ("docsms-metadata-{0}-{1}" -f $BuildId, (Get-Date -Format "yyyyMMdd-HHmmss"))
    )

    foreach ($path in @(
            $workspaceRoot,
            [System.IO.Path]::Combine($workspaceRoot, "sdk"),
            [System.IO.Path]::Combine($workspaceRoot, "docs"),
            [System.IO.Path]::Combine($workspaceRoot, "package-info"),
            [System.IO.Path]::Combine($workspaceRoot, "tooling"),
            [System.IO.Path]::Combine($workspaceRoot, "logs")
        )) {
        New-Item -ItemType Directory -Path $path | Out-Null
    }

    return [PSCustomObject]@{
        Root              = $workspaceRoot
        Sdk               = [System.IO.Path]::Combine($workspaceRoot, "sdk")
        Docs              = [System.IO.Path]::Combine($workspaceRoot, "docs")
        PackageInfo       = [System.IO.Path]::Combine($workspaceRoot, "package-info")
        Tooling           = [System.IO.Path]::Combine($workspaceRoot, "tooling")
        Logs              = [System.IO.Path]::Combine($workspaceRoot, "logs")
    }
}

function Clone-Repository {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RepositoryUrl,

        [Parameter(Mandatory = $true)]
        [string] $DestinationPath,

        [Parameter(Mandatory = $true)]
        [string] $Ref,

        [Parameter(Mandatory = $true)]
        [string[]] $SparseCheckoutPaths,

        [Parameter(Mandatory = $true)]
        [string] $FriendlyName
    )

    Invoke-NativeCommand `
        -FilePath "git" `
        -Arguments @("clone", "--no-checkout", "--filter=blob:none", $RepositoryUrl, $DestinationPath) `
        -FailureMessage "Failed to clone the $FriendlyName repository." `
        -ScriptExitCode 4

    Invoke-NativeCommand `
        -FilePath "git" `
        -Arguments @("sparse-checkout", "init", "--cone") `
        -ExecutePath $DestinationPath `
        -FailureMessage "Failed to initialize sparse checkout for the $FriendlyName repository." `
        -ScriptExitCode 4

    Invoke-NativeCommand `
        -FilePath "git" `
        -Arguments (@("sparse-checkout", "set") + $SparseCheckoutPaths) `
        -ExecutePath $DestinationPath `
        -FailureMessage "Failed to configure sparse checkout paths for the $FriendlyName repository." `
        -ScriptExitCode 4

    $fetchExitCode = Invoke-NativeCommand `
        -FilePath "git" `
        -Arguments @("fetch", "--depth", "1", "origin", $Ref) `
        -ExecutePath $DestinationPath `
        -FailureMessage "Failed to fetch ref '$Ref' for the $FriendlyName repository." `
        -ScriptExitCode 4 `
        -DoNotThrow

    if ($fetchExitCode) {
        LogWarning "Direct fetch of ref '$Ref' for the $FriendlyName repository failed. Attempting checkout with the cloned refs."
    }

    Invoke-NativeCommand `
        -FilePath "git" `
        -Arguments @("checkout", $Ref) `
        -ExecutePath $DestinationPath `
        -FailureMessage "Failed to checkout ref '$Ref' for the $FriendlyName repository." `
        -ScriptExitCode 4
}

function Get-PythonExecutablePath {
    foreach ($commandName in @("python3", "python")) {
        $command = Get-Command $commandName -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($command) {
            return $command.Source
        }
    }

    Throw-ScriptFailure "Python was not found. Install Python 3 before running the docs metadata update for Python packages." 5
}

function Get-VenvScriptDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $VenvPath
    )

    if ($IsWindows) {
        return [System.IO.Path]::Combine($VenvPath, "Scripts")
    }

    return [System.IO.Path]::Combine($VenvPath, "bin")
}

function Initialize-JavascriptPrerequisites {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SdkRoot,

        [Parameter(Mandatory = $true)]
        [string] $ToolingRoot
    )

    if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
        Throw-ScriptFailure "Node.js was not found. Install Node.js before running the docs metadata update for JavaScript packages." 5
    }

    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        Throw-ScriptFailure "npm was not found. Install Node.js (which includes npm) before running the docs metadata update for JavaScript packages." 5
    }

    $versionFile = [System.IO.Path]::Combine($SdkRoot, "eng", "scripts", "docs", "type2docfx.version.txt")
    if (-not (Test-Path $versionFile)) {
        Throw-ScriptFailure "Could not find the type2docfx version file at '$versionFile'." 5
    }

    $toolVersion = (Get-Content -Path $versionFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($toolVersion)) {
        Throw-ScriptFailure "The type2docfx version file at '$versionFile' was empty." 5
    }

    $npmPrefix = [System.IO.Path]::Combine($ToolingRoot, "node")
    $npmConfigPath = [System.IO.Path]::Combine($npmPrefix, ".npmrc")
    New-Item -ItemType Directory -Path $npmPrefix | Out-Null
    Set-Content -Path $npmConfigPath -Value @(
        "registry=$(Get-JavascriptPublicDevOpsRegistry)"
    )

    Invoke-NativeCommand `
        -FilePath "npm" `
        -Arguments @(
            "install",
            "--prefix", $npmPrefix,
            "--userconfig", $npmConfigPath,
            "--registry", (Get-JavascriptPublicDevOpsRegistry),
            "@microsoft/type2docfx@$toolVersion"
        ) `
        -FailureMessage "Failed to install @microsoft/type2docfx into the isolated tooling directory." `
        -ScriptExitCode 5

    $localNodeBin = [System.IO.Path]::Combine($npmPrefix, "node_modules", ".bin")
    $env:PATH = "$localNodeBin$([System.IO.Path]::PathSeparator)$env:PATH"
    $env:npm_config_userconfig = $npmConfigPath
    $env:npm_config_prefix = $npmPrefix

    Invoke-NativeCommand `
        -FilePath "type2docfx" `
        -Arguments @("--help") `
        -FailureMessage "The isolated type2docfx installation could not be executed." `
        -ScriptExitCode 5
}

function Initialize-PythonPrerequisites {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SdkRoot,

        [Parameter(Mandatory = $true)]
        [string] $ToolingRoot
    )

    $pythonExecutable = Get-PythonExecutablePath
    $venvPath = [System.IO.Path]::Combine($ToolingRoot, "python-venv")
    $requirementsPath = [System.IO.Path]::Combine($SdkRoot, "eng", "scripts", "docs", "py2docfx_requirements.txt")

    if (-not (Test-Path $requirementsPath)) {
        Throw-ScriptFailure "Could not find the py2docfx requirements file at '$requirementsPath'." 5
    }

    Invoke-NativeCommand `
        -FilePath $pythonExecutable `
        -Arguments @("-m", "venv", $venvPath) `
        -FailureMessage "Failed to create the isolated Python virtual environment." `
        -ScriptExitCode 5

    $venvScripts = Get-VenvScriptDirectory -VenvPath $venvPath
    $venvPython = [System.IO.Path]::Combine($venvScripts, $(if ($IsWindows) { "python.exe" } else { "python" }))
    $env:PATH = "$venvScripts$([System.IO.Path]::PathSeparator)$env:PATH"
    $env:VIRTUAL_ENV = $venvPath
    $env:PIP_INDEX_URL = Get-PythonPublicPackageIndex

    Invoke-NativeCommand `
        -FilePath $venvPython `
        -Arguments @("-m", "pip", "install", "-r", $requirementsPath, "--index-url", (Get-PythonPublicPackageIndex)) `
        -FailureMessage "Failed to install py2docfx requirements into the isolated Python virtual environment." `
        -ScriptExitCode 5

    Invoke-NativeCommand `
        -FilePath $venvPython `
        -Arguments @("-m", "py2docfx", "-h") `
        -FailureMessage "The isolated py2docfx installation could not be executed." `
        -ScriptExitCode 5
}

function Initialize-JavaPrerequisites {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SdkRoot,

        [Parameter(Mandatory = $true)]
        [string] $ToolingRoot
    )

    if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
        Throw-ScriptFailure "Java was not found. Install Java before running the docs metadata update for Java packages." 5
    }

    if (-not (Get-Command mvn -ErrorAction SilentlyContinue)) {
        Throw-ScriptFailure "Maven was not found. Install Maven before running the docs metadata update for Java packages." 5
    }

    $versionFile = [System.IO.Path]::Combine($SdkRoot, "eng", "scripts", "docs", "java2docfx.version.txt")
    if (-not (Test-Path $versionFile)) {
        Throw-ScriptFailure "Could not find the java2docfx version file at '$versionFile'." 5
    }

    $toolVersion = (Get-Content -Path $versionFile -Raw).Trim()
    if ([string]::IsNullOrWhiteSpace($toolVersion)) {
        Throw-ScriptFailure "The java2docfx version file at '$versionFile' was empty." 5
    }

    $buildBinariesDirectory = [System.IO.Path]::Combine($ToolingRoot, "build-binaries")
    $java2docfxDirectory = [System.IO.Path]::Combine($buildBinariesDirectory, "java2docfx")
    New-Item -ItemType Directory -Path $buildBinariesDirectory | Out-Null
    New-Item -ItemType Directory -Path $java2docfxDirectory | Out-Null
    $env:BUILD_BINARIESDIRECTORY = $buildBinariesDirectory

    Invoke-NativeCommand `
        -FilePath "mvn" `
        -Arguments @("org.apache.maven.plugins:maven-help-plugin:help", "--batch-mode") `
        -FailureMessage "Failed to warm the Maven help plugin cache for java2docfx." `
        -ScriptExitCode 5

    Invoke-NativeCommand `
        -FilePath "mvn" `
        -Arguments @(
            "dependency:copy",
            "-Dartifact=com.microsoft:java2docfx:$toolVersion",
            "-DoutputDirectory=$java2docfxDirectory"
        ) `
        -ExecutePath $java2docfxDirectory `
        -FailureMessage "Failed to download java2docfx into the isolated tooling directory." `
        -ScriptExitCode 5

    $java2docfxJar = [System.IO.Path]::Combine($java2docfxDirectory, "java2docfx-$toolVersion.jar")
    if (-not (Test-Path $java2docfxJar)) {
        Throw-ScriptFailure "java2docfx was not downloaded to '$java2docfxJar'." 5
    }

    Invoke-NativeCommand `
        -FilePath "java" `
        -Arguments @("-jar", $java2docfxJar, "-h") `
        -FailureMessage "The isolated java2docfx installation could not be executed." `
        -ScriptExitCode 5
}

function Initialize-LanguagePrerequisites {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [string] $SdkRoot,

        [Parameter(Mandatory = $true)]
        [string] $ToolingRoot
    )

    switch ($RepositoryConfiguration.Ecosystem) {
        "js" {
            Initialize-JavascriptPrerequisites -SdkRoot $SdkRoot -ToolingRoot $ToolingRoot
        }
        "python" {
            Initialize-PythonPrerequisites -SdkRoot $SdkRoot -ToolingRoot $ToolingRoot
        }
        "java" {
            Initialize-JavaPrerequisites -SdkRoot $SdkRoot -ToolingRoot $ToolingRoot
        }
        "net" {
            return
        }
        "cpp" {
            return
        }
        default {
            Throw-ScriptFailure "Unexpected ecosystem '$($RepositoryConfiguration.Ecosystem)'." 5
        }
    }
}

function Get-PackageSourceOverride {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [array] $DownloadedPackageInfos
    )

    if ($RepositoryConfiguration.Ecosystem -notin @("js", "python")) {
        return $null
    }

    $hasDevVersion = $false
    foreach ($downloadedPackage in $DownloadedPackageInfos) {
        if (Get-OptionalPropertyValue -InputObject $downloadedPackage.PackageInfo -PropertyName "DevVersion") {
            $hasDevVersion = $true
            break
        }
    }

    if ($hasDevVersion) {
        return $RepositoryConfiguration.PackageSourceOverride
    }

    return $null
}

function Get-PwshExecutablePath {
    $pwshCommand = Get-Command pwsh -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pwshCommand) {
        return $pwshCommand.Source
    }

    Throw-ScriptFailure "pwsh was not found on PATH even though this script is running under PowerShell." 6
}

function Invoke-DocsMetadataUpdate {
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [string] $SdkRoot,

        [Parameter(Mandatory = $true)]
        [string] $DocsRoot,

        [Parameter(Mandatory = $true)]
        [string[]] $PackageInfoJsonLocations,

        [string] $PackageSourceOverride
    )

    $updateScriptPath = [System.IO.Path]::Combine($SdkRoot, "eng", "common", "scripts", "Update-DocsMsMetadata.ps1")
    if (-not (Test-Path $updateScriptPath)) {
        Throw-ScriptFailure "Could not find Update-DocsMsMetadata.ps1 at '$updateScriptPath'." 6
    }

    $arguments = @(
        "-File", $updateScriptPath,
        "-PackageInfoJsonLocations"
    )
    $arguments += $PackageInfoJsonLocations
    $arguments += @(
        "-DocRepoLocation", $DocsRoot,
        "-Language", $RepositoryConfiguration.Language,
        "-RepoId", $RepositoryConfiguration.SdkRepository
    )

    if ($PackageSourceOverride) {
        $arguments += @("-PackageSourceOverride", $PackageSourceOverride)
    }

    Invoke-NativeCommand `
        -FilePath (Get-PwshExecutablePath) `
        -Arguments $arguments `
        -ExecutePath $SdkRoot `
        -FailureMessage "Update-DocsMsMetadata.ps1 failed." `
        -ScriptExitCode 6
}

function Get-DocsRepositoryStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string] $DocsRoot
    )

    LogGroupStart "git --no-pager status --short"
    try {
        Push-Location $DocsRoot
        $statusLines = git --no-pager status --short
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
        LogGroupEnd
    }

    if ($exitCode) {
        Throw-ScriptFailure "Failed to query git status for the cloned docs repository." 6
    }

    return @($statusLines)
}

function Write-RunSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string] $WorkspaceRoot,

        [Parameter(Mandatory = $true)]
        [PSCustomObject] $RepositoryConfiguration,

        [Parameter(Mandatory = $true)]
        [string[]] $RequestedPackages,

        [Parameter(Mandatory = $true)]
        [string[]] $ChangedFiles
    )

    Write-Host ""
    LogSuccess "Docs metadata update completed."
    Write-Host "Workspace: $WorkspaceRoot"
    Write-Host "SDK repo:   $($RepositoryConfiguration.SdkRepository)"
    Write-Host "Docs repo:  $($RepositoryConfiguration.DocsRepository)"
    Write-Host "Packages:   $($RequestedPackages -join ', ')"
    Write-Host ""
    Write-Host "Changed files:"

    if ($ChangedFiles.Count -eq 0) {
        Write-Host "  (no changes)"
        return
    }

    foreach ($line in $ChangedFiles) {
        Write-Host "  $line"
    }
}

function Main {
    $normalizedPackageNames = Normalize-PackageNames -PackageNames $PackageNames
    $buildContext = Get-BuildContextFromUrl -BuildUrl $BuildUrl
    $headers = @{
        Authorization = "Bearer $(Get-AdoBearerToken)"
    }

    $build = Invoke-AdoGet `
        -Organization $buildContext.Organization `
        -Project $buildContext.Project `
        -Path "/build/builds/$($buildContext.BuildId)" `
        -Headers $headers

    $buildRepositoryId = Get-BuildRepositoryId -Build $build
    $repositoryConfiguration = Get-EcosystemConfigurationFromBuildRepo -BuildRepositoryId $buildRepositoryId
    $requestedPackageContexts = Get-RequestedPackageContexts `
        -RepositoryConfiguration $repositoryConfiguration `
        -PackageNames $normalizedPackageNames

    $artifactsResponse = Invoke-AdoGet `
        -Organization $buildContext.Organization `
        -Project $buildContext.Project `
        -Path "/build/builds/$($buildContext.BuildId)/artifacts" `
        -Headers $headers

    $artifacts = @(
        foreach ($artifact in @($artifactsResponse.value)) {
            $artifactResource = Get-OptionalPropertyValue -InputObject $artifact -PropertyName "resource"
            $downloadUrl = Get-OptionalPropertyValue -InputObject $artifactResource -PropertyName "downloadUrl"
            if (-not [string]::IsNullOrWhiteSpace("$downloadUrl")) {
                $artifact
            }
        }
    )
    if ($artifacts.Count -eq 0) {
        Throw-ScriptFailure "The build did not expose any downloadable artifacts." 3
    }

    $workspace = New-WorkspaceLayout -BuildId $buildContext.BuildId

    $downloadedPackageInfos = foreach ($packageContext in $requestedPackageContexts) {
        Download-PackageInfoJson `
            -RepositoryConfiguration $repositoryConfiguration `
            -PackageContext $packageContext `
            -Artifacts $artifacts `
            -Headers $headers `
            -PackageInfoDirectory $workspace.PackageInfo
    }

    $sdkSparsePaths = Get-SdkSparseCheckoutPaths -DownloadedPackageInfos $downloadedPackageInfos
    Clone-Repository `
        -RepositoryUrl "https://github.com/$($repositoryConfiguration.SdkRepository).git" `
        -DestinationPath $workspace.Sdk `
        -Ref "$($build.sourceVersion)" `
        -SparseCheckoutPaths $sdkSparsePaths `
        -FriendlyName "SDK"

    Clone-Repository `
        -RepositoryUrl "https://github.com/$($repositoryConfiguration.DocsRepository).git" `
        -DestinationPath $workspace.Docs `
        -Ref "main" `
        -SparseCheckoutPaths $repositoryConfiguration.DocsSparsePaths `
        -FriendlyName "docs"

    Initialize-LanguagePrerequisites `
        -RepositoryConfiguration $repositoryConfiguration `
        -SdkRoot $workspace.Sdk `
        -ToolingRoot $workspace.Tooling

    $packageSourceOverride = Get-PackageSourceOverride `
        -RepositoryConfiguration $repositoryConfiguration `
        -DownloadedPackageInfos $downloadedPackageInfos

    Invoke-DocsMetadataUpdate `
        -RepositoryConfiguration $repositoryConfiguration `
        -SdkRoot $workspace.Sdk `
        -DocsRoot $workspace.Docs `
        -PackageInfoJsonLocations @($downloadedPackageInfos | ForEach-Object { $_.Path }) `
        -PackageSourceOverride $packageSourceOverride

    $changedFiles = Get-DocsRepositoryStatus -DocsRoot $workspace.Docs
    Write-RunSummary `
        -WorkspaceRoot $workspace.Root `
        -RepositoryConfiguration $repositoryConfiguration `
        -RequestedPackages @($requestedPackageContexts | ForEach-Object { $_.RequestedName }) `
        -ChangedFiles $changedFiles
}

try {
    Main
}
catch {
    $exitCode = 1
    if ($_.Exception.Data.Contains("ExitCode")) {
        $exitCode = [int] $_.Exception.Data["ExitCode"]
    }

    LogError $_.Exception.Message
    exit $exitCode
}
