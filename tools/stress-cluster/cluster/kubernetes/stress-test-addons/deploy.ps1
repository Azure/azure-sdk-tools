$ErrorActionPreference = 'Stop'
Set-PSDebug -trace 1
$subscriptionId = "2cd617ea-1866-46b1-90e3-fffb087ebf9b"
$env:AZURE_STORAGE_ACCOUNT="stresstestcharts"
$env:AZURE_STORAGE_KEY=$(az storage account keys list --subscription $subscriptionId --account-name $env:AZURE_STORAGE_ACCOUNT -o tsv --query '[0].value')
if($LASTEXITCODE) {exit $LASTEXITCODE}

Remove-Item -Force ./*.tgz
if($LASTEXITCODE) {exit $LASTEXITCODE}

helm package  .
if($LASTEXITCODE) {exit $LASTEXITCODE}
helm repo index --url https://stresstestcharts.blob.core.windows.net/helm/ --merge index.yaml .
if($LASTEXITCODE) {exit $LASTEXITCODE}

# The index.yaml in git should be synced with the index.yaml already in blob storage
# az storage blob download -c helm -n index.yaml -f index.yaml

$files = (Get-Item *.tgz).Name
az storage blob upload --subscription $subscriptionId --container-name helm --file index.yaml --name index.yaml
if($LASTEXITCODE) {exit $LASTEXITCODE}
az storage blob upload --subscription $subscriptionId --container-name helm --file $files --name $files
if($LASTEXITCODE) {exit $LASTEXITCODE}

# index.yaml must be kept up to date, otherwise when helm generates the file, it will not
# merge it with previous entries, and those packages will become inaccessible as they are no
# longer index.
echo "COMMIT CHANGES MADE TO 'index.yaml'"
