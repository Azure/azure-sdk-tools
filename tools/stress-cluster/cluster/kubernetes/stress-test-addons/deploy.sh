export AZURE_STORAGE_ACCOUNT=stresstestcharts
# AZURE_STORAGE_KEY must be exported too

rm *.tgz
rm index.yaml

helm package  .
helm repo index --url https://stresstestcharts.blob.core.windows.net/helm/ .

az storage blob upload --container-name helm --file index.yaml --name index.yaml
az storage blob upload --container-name helm --file *.tgz --name *.tgz
