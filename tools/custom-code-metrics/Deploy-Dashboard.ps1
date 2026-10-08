#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = "Hosted")]
param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [string]$ResourceGroup = "rg-azsdk-custom-code-metrics",
    [string]$SiteName = "azsdk-custom-code-metrics",
    [Parameter(Mandatory, ParameterSetName = "Hosted")][uri]$IndexUrl,
    [Parameter(Mandatory, ParameterSetName = "Preview")][ValidateNotNullOrEmpty()][string[]]$SnapshotPath
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 4
if ($PSCmdlet.ParameterSetName -eq "Hosted" -and
    ($IndexUrl.Scheme -ne "https" -or $IndexUrl.UserInfo -or $IndexUrl.Query -or $IndexUrl.Fragment)) {
    throw "A public HTTPS reporting index without credentials is required."
}
$buildArguments = @("--index-url")
if ($PSCmdlet.ParameterSetName -eq "Preview") {
    $buildArguments = @("--preview")
    foreach ($path in $SnapshotPath) {
        if (-not [System.IO.Path]::IsPathFullyQualified($path) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Static preview requires an absolute path to each existing snapshot file."
        }
        $buildArguments += @("--snapshot", (Resolve-Path -LiteralPath $path).Path)
    }
} else {
    $buildArguments += $IndexUrl.AbsoluteUri
}
if (-not $PSCmdlet.ShouldProcess("$SubscriptionId/$ResourceGroup/$SiteName", "Build and replace the production dashboard")) {
    return
}
$previous = $env:SWA_CLI_DEPLOYMENT_TOKEN
$previousDebug = $env:SWA_CLI_DEBUG
Push-Location $PSScriptRoot
try {
    & node .\dashboard\build.mjs @buildArguments
    if ($LASTEXITCODE -ne 0) { throw "Dashboard build failed." }
    if ($PSCmdlet.ParameterSetName -eq "Hosted") {
        & node --input-type=module -e 'import {loadReport,loadHistory} from "./dashboard/generated/report.mjs"; await loadHistory(await loadReport(process.argv[1]),90);' $IndexUrl.AbsoluteUri
        if ($LASTEXITCODE -ne 0) { throw "Public reporting preflight failed. Publish complete data or configure an approved private-storage reader API first." }
    }
    $token = & az staticwebapp secrets list --name $SiteName --resource-group $ResourceGroup `
        --subscription $SubscriptionId --query properties.apiKey --output tsv --only-show-errors
    if ($LASTEXITCODE -ne 0 -or -not $token) { throw "Could not acquire the Static Web Apps deployment token." }
    $env:SWA_CLI_DEPLOYMENT_TOKEN = $token
    $env:SWA_CLI_DEBUG = "log"
    & npx --no-install swa deploy .\dashboard\dist --env production `
        --subscription-id $SubscriptionId --resource-group $ResourceGroup --app-name $SiteName --no-use-keychain
    if ($LASTEXITCODE -ne 0) { throw "Static Web Apps deployment failed." }
}
finally {
    $env:SWA_CLI_DEPLOYMENT_TOKEN = $previous
    $env:SWA_CLI_DEBUG = $previousDebug
    Pop-Location
}
