FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

COPY ./src /src

RUN cd /src && dotnet publish -c Release -o /stresswatcher -f net7.0

FROM mcr.microsoft.com/dotnet/runtime:7.0

COPY --from=build /stresswatcher /stresswatcher

WORKDIR /stresswatcher
ENTRYPOINT ["dotnet", "Stress.Watcher.dll"]
