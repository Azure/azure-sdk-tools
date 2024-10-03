FROM mcr.microsoft.com/dotnet/sdk:8.0-cbl-mariner2.0 AS build

RUN yum update -y

COPY ./src /src

RUN cd /src && dotnet publish -c Release -o /stresswatcher -r linux-x64 -f net8.0 -p:PublishSingleFile=true --self-contained

FROM mcr.microsoft.com/azure-cli:cbl-mariner2.0

COPY --from=build /stresswatcher /stresswatcher

WORKDIR /stresswatcher
ENTRYPOINT ["./Stress.Watcher.dll"]
