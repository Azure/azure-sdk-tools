$ErrorActionPreference = "Stop"

$subscriptionID="7fd08dcc-a653-4b0f-8f8c-4dac889fdda4"
$vaultName="avskeys"

# echo "Refreshing Azure subscription cache..."
# az account list --refresh | Out-Null
echo "Generating *.pem from Azure KeyVault..."

$secrets = az keyvault secret list --subscription $subscriptionID --vault-name $vaultName --output json | ConvertFrom-Json

foreach($secret in $secrets) {
    $name = $secret.name
    $value = az keyvault secret show --subscription $subscriptionID --vault-name $vaultName --name $name --query 'value' --output tsv
    Write-Host "Adding secret: $name"

    $arr = $value.split(' ')
    "-----BEGIN RSA PRIVATE KEY-----" | Out-File -FilePath .ssh/$name".pem" -Encoding ascii
    foreach($line in $arr) {
        "$line" | Out-File -Append -FilePath .ssh/$name".pem" -Encoding ascii
    }
    "-----END RSA PRIVATE KEY-----" | Out-File -Append -FilePath .ssh/$name".pem" -Encoding ascii
}

Write-Host "Done"
