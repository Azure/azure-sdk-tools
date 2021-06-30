set -ex

az login
az aks get-credentials -n rg-stress-test-cluster- -n stress-test

kubectl create namespace examples || true
helm install $(date +"%m%d%H%M%S") \
    -n examples \
     -f ../../../cluster/kubernetes/environments/test.yaml \
    ./chart
