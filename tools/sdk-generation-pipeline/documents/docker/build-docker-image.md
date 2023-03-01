If you want to rebuild the docker image, you only can do it in your local. It's suggested to create a release pipeline to build it. (Previously we find that sometimes it will be timeout because the pipeline costs much time in building and pushing docker image, but now the agent pool should be very fast.)

Steps to rebuild the docker image:
1. `cd tools/sdk-generation-pipeline`
2. `rush update`
3. `rush rebuild`
4. `cd packages/sdk-generation-cli`
5. `rushx pack`
6. `cd ../.. # go to tools/sdk-generation-pipeline`
7. `docker build -t sdkgeneration.azurecr.io/sdk-generation:beta-1.0 .`
