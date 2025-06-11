# Microsoft.FxLister

A command line tool that analyzes Azure NuGet packages and extracts type information.

## Overview

Microsoft.FxLister is a .NET 8.0 console application that discovers NuGet packages, downloads and analyzes their assemblies, and extracts public type names. The tool is particularly designed for analyzing Azure SDK libraries.

## Features

- Discovers NuGet packages using customizable regex patterns
- Downloads and analyzes package assemblies
- Extracts public type names (both short names and fully qualified names)
- Generates two output files:
  - `.txt`: Alphabetically sorted type names without namespaces
  - `.qualified.txt`: Fully qualified type names with namespace and package information

## Installation

Build the project using .NET 8.0:

```bash
cd src/dotnet/Microsoft.FxLister
dotnet build
```

## Usage

### Basic Usage

```bash
fxlister types -o output.txt
```

This will generate:
- `output.txt`: Sorted type names
- `output.qualified.txt`: Fully qualified type names

### Command Options

- `-o, --output` (required): Output file path (without extension)
- `-m, --max-packages`: Maximum number of packages to process (default: 100)
- `-p, --package-pattern`: Regex pattern to filter package names

### Examples

#### Select Azure SDK Libraries (excluding ResourceManager and Provisioning)

```bash
fxlister types -o azure-types -p "^Azure\.(?!ResourceManager)(?!Provisioning)" -m 50
```

#### Select only Azure Storage packages

```bash
fxlister types -o storage-types -p "^Azure\.Storage" -m 20
```

#### Select all Azure packages (including ResourceManager)

```bash
fxlister types -o all-azure-types -p "^Azure\." -m 200
```

#### Select Azure Identity and Key Vault packages

```bash
fxlister types -o identity-keyvault-types -p "^Azure\.(Identity|KeyVault)" -m 10
```

## Output Format

### Short Names File (.txt)
```
AccessToken
BlobClient
DefaultAzureCredential
KeyVaultSecret
StorageSharedKeyCredential
```

### Qualified Names File (.qualified.txt)
```
Azure.Core.AccessToken in Azure.Core
Azure.Storage.Blobs.BlobClient in Azure.Storage.Blobs
Azure.Identity.DefaultAzureCredential in Azure.Identity
Azure.Security.KeyVault.Secrets.KeyVaultSecret in Azure.Security.KeyVault.Secrets
Azure.Storage.StorageSharedKeyCredential in Azure.Storage.Common
```

## Regex Pattern Examples

The package pattern option uses .NET regular expressions to filter package names:

- `^Azure\.` - All packages starting with "Azure."
- `^Azure\.(?!ResourceManager)(?!Provisioning)` - Azure packages excluding ResourceManager and Provisioning (default)
- `^Azure\.Storage` - Only Azure Storage packages
- `^Azure\.(Identity|KeyVault)` - Azure Identity and KeyVault packages
- `^Azure\..*(?<!Test)$` - Azure packages not ending with "Test"

## Technical Details

The tool consists of:
- **PackageAnalyzer**: Discovers NuGet packages using NuGet APIs
- **RealTypeExtractor**: Analyzes assemblies using reflection metadata
- **TypesCommand**: Command-line interface using System.CommandLine

The tool targets .NET Standard 2.0 assemblies and builds as a single-file executable for easy distribution.