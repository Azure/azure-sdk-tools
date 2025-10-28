FROM mcr.microsoft.com/dotnet/sdk:9.0

ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

RUN apt-get update \
    && apt-get install -y curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

RUN dotnet --version && node --version && npm --version

WORKDIR /app

COPY . .

# setup azsdk-cli
RUN dotnet restore ./azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj
RUN dotnet build ./azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj --self-contained
RUN dotnet publish ./azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj -c Release -o ./azsdk-cli/publish-linux --self-contained

# setup node server
RUN npm install

CMD [ "npm", "run", "start" ]