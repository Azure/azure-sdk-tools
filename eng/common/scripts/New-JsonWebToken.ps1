#requires -Version 7.0
using namespace System.Security.Cryptography
using namespace System.Text

param(
    $Payload,
    [string]$PrivateKeyPem
)

function ConvertTo-Base64($Value) {
    if ($Value -is [string]) {
        $Value = [System.Text.Encoding]::UTF8.GetBytes($Value)
    }

    if ($value -isnot [byte[]]) {
        throw "Value must be a string or byte array"
    }

    $output = [Convert]::ToBase64String($Value).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    return $output
}

$header = @{
    "alg" = "RS256"
    "typ" = "JWT"
}

$rsa = [RSA]::Create()
$rsa.ImportFromPem($privateKeyPem)

$headerEncoded  = ConvertTo-Base64 -Value ($header | ConvertTo-Json -Compress)
$payloadEncoded = ConvertTo-Base64 -Value ($payload | ConvertTo-Json -Compress)
$signatureEncoded = ConvertTo-Base64 -Value $rsa.SignData([Encoding]::UTF8.GetBytes("$headerEncoded.$payloadEncoded"), [HashAlgorithmName]::SHA256, [RSASignaturePadding]::Pkcs1)

return "$headerEncoded.$payloadEncoded.$signatureEncoded"