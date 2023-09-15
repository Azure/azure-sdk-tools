function Run()
{
    Write-Host "`n==> $args`n" -ForegroundColor Green
    $command, $arguments = $args
    & $command $arguments
    if ($LASTEXITCODE) {
        Write-Error "Command '$args' failed with code: $LASTEXITCODE" -ErrorAction 'Continue'
    }
}
function RunOrExitOnFailure()
{
    Run @args
    if ($LASTEXITCODE) {
        exit $LASTEXITCODE
    }
}
$ErrorActionPreference = 'Stop'
$subscriptionId = "2cd617ea-1866-46b1-90e3-fffb087ebf9b"
$env:AZURE_STORAGE_ACCOUNT="stresstestcharts"
$env:AZURE_STORAGE_KEY=$(RunOrExitOnFailure az storage account keys list --subscription $subscriptionId --account-name $env:AZURE_STORAGE_ACCOUNT -o tsv --query '[0].value')

Remove-Item -Force ./*.tgz

RunOrExitOnFailure helm package  .
RunOrExitOnFailure helm repo index --url https://stresstestcharts.blob.core.windows.net/helm/ --merge index.yaml .

# The index.yaml in git should be synced with the index.yaml already in blob storage
# az storage blob download -c helm -n index.yaml -f index.yaml

$files = (Get-Item *.tgz).Name
$confirmation = Read-Host "Do you want to update the helm repository to add ${files}? [y/n]"
if ( $confirmation -match "[yY]" ) { 
    RunOrExitOnFailure az storage blob upload --subscription $subscriptionId --container-name helm --file index.yaml --name index.yaml --overwrite
    RunOrExitOnFailure az storage blob upload --subscription $subscriptionId --container-name helm --file $files --name $files

    # index.yaml must be kept up to date, otherwise when helm generates the file, it will not
    # merge it with previous entries, and those packages will become inaccessible as they are no
    # longer index.
    Write-Host "UPDATE CHANGELOG.md"
    Write-Host "COMMIT CHANGES MADE TO 'index.yaml' and 'CHANGELOG.md'"
} else {
    Write-Host "Abort uploading files $files."
}
