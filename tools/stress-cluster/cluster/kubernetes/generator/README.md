# Stress Test Generator

This directory contains a CLI program for quickly generating a [stress test package](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md#creating-a-stress-test).

## Usage

*Dependencies*:

- [.NET runtime](https://dotnet.microsoft.com/download)

*To install the generator*:

```
dotnet tool install stress.generator --global --version '1.0.0-dev.*' --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json
```

*To run the generator*:

```
mkdir <stress test package dir>
stress-test-generator -d <stress test package dir>
```

The tool will ask a series of prompts to help generate the stress test package configuration and layout.

After generation, see the docs on [Creating a Stress Test](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md#creating-a-stress-test) and [Deploying a Stress Test](https://github.com/Azure/azure-sdk-tools/blob/main/tools/stress-cluster/chaos/README.md#deploying-a-stress-test) to add and exercise your test code.

## Development

*Dev dependencies*:
- [Dotnet SDK](https://dotnet.microsoft.com/download)

*To build and run*:

```
# Omit the command line flags to generate in current working directory
dotnet run -p ./Stress.Generator -- -d <output directory>
```

*To test*:

```
dotnet test
```
