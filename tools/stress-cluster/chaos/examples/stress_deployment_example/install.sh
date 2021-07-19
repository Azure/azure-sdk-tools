set -ex

helm dependency update
az bicep build -f ./test-resources.bicep

az login
az aks get-credentials -n rg-stress-test-cluster- -n stress-test --subscription 'Azure SDK Test Resources'

kubectl create namespace examples || true
helm install stress-deploy-example -n examples . --set stress-test-addons.env=test
