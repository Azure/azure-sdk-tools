param (
    $inputPayload = "build\validation_small.txt"
)

$payloadContent = Get-Content -Path $inputPayload


$result = $payloadContent -replace "^}$", "},"
$result = $result -replace "`\`\`"", "`""
$result = $result -replace "`\`"", "`""
$result = $result -replace "`"", "`\`""
$result = $result -replace "message:\s\'", "`"message`": `""
$result = $result -replace "level:\s\'", "`"level`": `""
$result = $result -replace "`'}$", "`"}"
$result = $result -replace "`',$", "`","
$result = $result | % { $_.Replace("`\x1B`[31m", "") } 
$result = $result | % { $_.Replace("`\x1B`[39m", "") } 

$result = $result -replace "error`'", "error`""
$result = $result -replace "`'$", "`","

$result[-1] = "}]}"
$result = "{`n`"content`": [",$result

Set-Content -Path build/validation_formatted.json -Value $result
