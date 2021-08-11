export AZURE_STORAGE_ACCOUNT=stresstestcharts
export AZURE_STORAGE_KEY=$(az storage account keys list --account-name $AZURE_STORAGE_ACCOUNT -o tsv --query '[0].value')

rm *.tgz

helm package  .
helm repo index --url https://stresstestcharts.blob.core.windows.net/helm/ --merge index.yaml .

# The index.yaml in git should be synced with the index.yaml already in blob storage
# az storage blob download -c helm -n index.yaml -f index.yaml

az storage blob upload --container-name helm --file index.yaml --name index.yaml
az storage blob upload --container-name helm --file *.tgz --name *.tgz

# index.yaml must be kept up to date, otherwise when helm generates the file, it will not
# merge it with previous entries, and those packages will become inaccessible as they are no
# longer index.
echo "COMMIT CHANGES MADE TO `index.yaml`"
