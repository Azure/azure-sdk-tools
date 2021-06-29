set -ex

az login
az acr login stresstestregistry
az aks get-credentials -n rg-stress-test-cluster- -n stress-test

docker push stresstestregistry.azurecr.io/example/networkexample:v1

kubectl create namespace examples
helm install network-example -n examples ./chart -f ../../../cluster/kubernetes/environments/test.yaml
