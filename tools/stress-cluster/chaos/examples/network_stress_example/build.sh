set -ex

docker build . -t stresstestregistry.azurecr.io/example/networkexample:v1
helm dependency update ./chart
