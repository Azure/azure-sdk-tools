FROM mcr.microsoft.com/dotnet/sdk:6.0
# fault injector requires .NET 6

ARG FAULT_INJECTOR_VERSION="0.2.0-dev.20231009.1"
RUN dotnet tool install azure.sdk.tools.httpfaultinjector --version ${FAULT_INJECTOR_VERSION} --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json
RUN dotnet dev-certs https

EXPOSE 7777
EXPOSE 7778

ENTRYPOINT [ "/root/.dotnet/tools/http-fault-injector" ]


# Usage:
# - make changes (e.g. update versions)
# - Log in to container registry instance: `az acr login -n {registry}` (e.g. `azsdkengsys.azurecr.io`)
# - docker build . -t {registry}/dotnet-tools/httpfaultinjector:{new tag}, e.g. `0.2.0-dev`
# - docker push {registry}/dotnet-tools/httpfaultinjector:{new tag}
# - optional: to make others use new version, also add `latest` tag:
#   * be careful as it might break someone
#   * `docker tag {registry}/dotnet-tools/httpfaultinjector:{new tag} {registry}/dotnet-tools/httpfaultinjector:latest`
#   * `docker push {registry}/dotnet-tools/httpfaultinjector:latest`