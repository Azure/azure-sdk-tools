# Contributing to azsdk-cli

## Prerequisites

- [.NET 8.0 SDK or later](https://dotnet.microsoft.com/download)

## Setup

1. Clone the repository:
    ```sh
    git clone https://github.com/Azure/azure-sdk-tools.git
    cd azure-sdk-tools/tools/azsdk-cli
    ```

2. Restore dependencies:
    ```sh
    dotnet restore
    ```

## Build

To build the project:

```sh
dotnet build
```

## Run

To run the CLI locally:

```sh
dotnet run --project Azure.Sdk.Tools.Cli -- --help
```

## Test

To run the tests:

```sh
dotnet test
```

## Package

To create a self contained binary:

```sh
dotnet publish -f net8.0 -c Release -r linux-x64 -p:PublishSingleFile=true --self-contained --output ./artifacts/linux-x64 ./tools/azsdk-cli/Azure.Sdk.Tools.Cli/Azure.Sdk.Tools.Cli.csproj
```