FROM mcr.microsoft.com/dotnet/sdk:8.0-azurelinux3.0 AS build

RUN tdnf upgrade -y

COPY ./src /src

RUN cd /src && dotnet publish -c Release -o /stresswatcher -r linux-x64 -f net8.0 -p:PublishSingleFile=true --self-contained

FROM mcr.microsoft.com/azure-cli:azurelinux3.0

RUN tdnf upgrade -y

COPY --from=build /stresswatcher /stresswatcher

WORKDIR /stresswatcher
ENTRYPOINT ["./Stress.Watcher"]
