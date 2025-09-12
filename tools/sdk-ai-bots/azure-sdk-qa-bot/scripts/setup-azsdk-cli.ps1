#!/usr/bin/env pwsh

param(
    [string]$OS = "win32"
)

# setup azsdk-cli
dotnet restore ./azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj
dotnet build ./azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj --self-contained
dotnet publish ./azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj -c Release -o ./azsdk-cli/publish-${OS} --self-contained
