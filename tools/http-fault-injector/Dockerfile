FROM mcr.microsoft.com/dotnet/sdk:6.0
# fault injector requires .NET 6

ARG FAULT_INJECTOR_VERSION=""
ARG NUGET_SOURCE="https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json"
RUN if [ ${FAULT_INJECTOR_VERSION} = ""]; \
    then dotnet tool install azure.sdk.tools.httpfaultinjector --prerelease --global --add-source ${NUGET_SOURCE}; \
    else dotnet tool install azure.sdk.tools.httpfaultinjector --version ${FAULT_INJECTOR_VERSION} --global --add-source ${NUGET_SOURCE}; \    
    fi

RUN dotnet dev-certs https

EXPOSE 7777
EXPOSE 7778

ENTRYPOINT [ "/root/.dotnet/tools/http-fault-injector" ]


# Usage:
# - make changes (e.g. update versions)
# - Log in to container registry instance: `az acr login -n {registry}` (e.g. `azsdkengsys.azurecr.io`)
# - To build docker image:
#   - for the latest available version of fault injector run
#     docker build . -t {registry}/stress/httpfaultinjector:{new tag}
#   - for specific version of fault injector run
#     docker build . -t {registry}/stress/httpfaultinjector:{new tag} --build-arg FAULT_INJECTOR_VERSION=0.2.0-dev.20231009.1
#   - {new tag} should match the version like `0.2.0-dev`
# - docker push {registry}/stress/httpfaultinjector:{new tag}
# - optional: to make others use new version, also add `latest` tag:
#   * be careful as it might break someone
#   * `docker tag {registry}/stress/httpfaultinjector:{new tag} {registry}/stress/httpfaultinjector:latest`
#   * `docker push {registry}/stress/httpfaultinjector:latest`
