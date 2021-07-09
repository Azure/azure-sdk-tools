set -ex

helm dependency update

az login
az acr login stresstestregistry
az aks get-credentials -n rg-stress-test-cluster- -n stress-test --subscription 'Azure SDK Test Resources'

docker build . -t stresstestregistry.azurecr.io/example/networkexample:v1
docker push stresstestregistry.azurecr.io/example/networkexample:v1

kubectl create namespace examples || true
helm install stress-network-example -n examples . --set stress-test-addons.env=test
