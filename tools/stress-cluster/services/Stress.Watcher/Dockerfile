FROM mcr.microsoft.com/dotnet/sdk:6.0-cbl-mariner2.0 AS build

COPY ./src /src

RUN cd /src && dotnet publish -c Release -o /stresswatcher -f net6.0

FROM mcr.microsoft.com/dotnet/runtime:6.0-cbl-mariner2.0

COPY --from=build /stresswatcher /stresswatcher

WORKDIR /stresswatcher
ENTRYPOINT ["dotnet", "Stress.Watcher.dll"]
