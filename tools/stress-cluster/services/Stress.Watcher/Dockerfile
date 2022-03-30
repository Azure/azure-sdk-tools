FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

COPY ./src /src

RUN cd /src && dotnet publish -c Release -o /stresswatcher -f net5.0

FROM mcr.microsoft.com/dotnet/runtime:5.0

COPY --from=build /stresswatcher /stresswatcher

WORKDIR /stresswatcher
ENTRYPOINT ["dotnet", "Stress.Watcher.dll"]
