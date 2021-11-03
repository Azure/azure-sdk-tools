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

$environmentText = ''
foreach($entry in $DeploymentOutputs.GetEnumerator()) {
    $environmentText += "$($entry.name)=$($entry.value)`n"
}

$bytes = [System.Text.Encoding]::UTF8.GetBytes($environmentText)
Set-Content $outputFile -Value $bytes -AsByteStream -Force
