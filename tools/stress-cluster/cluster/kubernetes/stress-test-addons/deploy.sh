export AZURE_STORAGE_ACCOUNT=stresstestcharts
# AZURE_STORAGE_KEY must be exported too, run the below command to get the key:
# az storage account keys list --account-name stresstestcharts -o json --query '[0].value'

rm *.tgz

helm package  .
helm repo index --url https://stresstestcharts.blob.core.windows.net/helm/ .

az storage blob upload --container-name helm --file index.yaml --name index.yaml
az storage blob upload --container-name helm --file *.tgz --name *.tgz

# index.yaml must be kept up to date, otherwise when helm generates the file, it will not
# merge it with previous entries, and those packages will become inaccessible as they are no
# longer index.
echo "COMMIT CHANGES MADE TO `index.yaml`"
