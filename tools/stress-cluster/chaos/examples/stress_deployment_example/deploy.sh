set -ex

az login
az aks get-credentials -n rg-stress-test-cluster- -n stress-test

kubectl create namespace examples
helm install deploy-example -n examples ./chart -f ../../../cluster/kubernetes/environments/test.yaml
