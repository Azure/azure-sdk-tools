param (
    [Parameter()]
    [hashtable] $DeploymentOutputs,

    [Parameter(ValueFromRemainingArguments = $true)]
    $RemainingArguments
)

$outputFile = "$env:ENV_FILE"
if ([string]::IsNullOrEmpty($outputFile)) {
    throw "Output file path not specified as env variable `$env:ENV_FILE"
}

$duplicates = @{}
$envLines = @()

try {
    foreach ($line in (Get-Content $outputFile)) {
        $duplicates[$line] = $true
        $envLines += $line
    }
}
catch {}

foreach ($entry in $DeploymentOutputs.GetEnumerator()) {
    $line = "$($entry.name)=$($entry.value)"
    if (!$duplicates.Contains($line)) {
        $duplicates[$line] = $true
        $envLines += $line
    }
}

$environmentText = $envLines -join "`n"

$bytes = [System.Text.Encoding]::UTF8.GetBytes($environmentText)
Set-Content $outputFile -Value $bytes -AsByteStream -Force

function Write-Sourceable-BashFile([string]$sourceableBashFilePath) {
    Write-Output "Writing $sourceableBashFilePath"

    # the .sh file equivalent with all variables exported and properly quoted so it
    # can be sourced in a bash script.    
    $sourceableBashFileText = "# This file is intended to be sourced by a bash script`nExample: . $ENV_FILE + '.sh'"

    foreach ($envLine in $envLines) {
        Write-Output "Processing $envLine"
        # grab any lines that don't start with the comment char, and squeeze out the spaces between the key and value.
        # ex: "EVENTHUB_CONNECTION_STRING=<some connection string with embedded ='s, etc..>"
        if ($envLine -match "^([^#].+?)\s*=\s*(.+?)$") {        
            $sourceableBashFileText += [string]::Format("export {0}='{1}'`n", $Matches[1], $Matches[2])
        }
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($sourceableBashFileText)
    Set-Content $sourceableBashFilePath -Value $bytes -AsByteStream -Force
}

Write-Sourceable-BashFile ($outputFile + ".sh") $envLines
