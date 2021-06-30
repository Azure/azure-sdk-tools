set -ex

if [ -z "$1" ]
then
    echo "Using default namespace 'examples'"
    NAMESPACE="examples"
else
    echo "Using namespace '$1'"
    NAMESPACE="$1"
fi

#az login
#az acr login stresstestregistry
#az aks get-credentials -n rg-stress-test-cluster- -n stress-test
#
#docker push stresstestregistry.azurecr.io/example/networkexample:v1

kubectl create namespace $NAMESPACE || true
helm install $(date +"%m%d%H%M%S") \
    -n $NAMESPACE \
    -f ../../../cluster/kubernetes/environments/test.yaml \
    ./chart
