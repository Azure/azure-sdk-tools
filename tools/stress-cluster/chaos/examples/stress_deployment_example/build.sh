set -ex

az bicep build -f ./test-resources.bicep --outfile ./chart/test-resources.json
helm dependency update ./chart
