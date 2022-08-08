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
$environmentText = ''

try {
    foreach ($line in (Get-Content $outputFile)) {
        $duplicates[$line] = $true
        $environmentText += ($line + "`n")
    }
} catch {}

foreach ($entry in $DeploymentOutputs.GetEnumerator()) {
    $line = "$($entry.name)=$($entry.value)"
    if (!$duplicates.Contains($line)) {
        $duplicates[$line] = $true
        $environmentText += ("$line" + "`n")
    }
}

$bytes = [System.Text.Encoding]::UTF8.GetBytes($environmentText)
Set-Content $outputFile -Value $bytes -AsByteStream -Force
