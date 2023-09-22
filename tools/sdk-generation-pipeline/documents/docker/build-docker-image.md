## Rebuild docker In Local
Steps to rebuild the docker image:
1. `cd tools/sdk-generation-pipeline`
2. `rush update`
3. `rush rebuild`
4. `cd packages/sdk-generation-cli`
5. `rushx pack`
6. `cd ../.. # go to tools/sdk-generation-pipeline`
7. `docker build -t sdkgeneration.azurecr.io/sdk-generation:beta-1.0 .`
7. `docker push sdkgeneration.azurecr.io/sdk-generation:beta-1.0`
## Rebuild docker In Pipeline
https://dev.azure.com/azure-sdk/internal/_build?definitionId=5980
