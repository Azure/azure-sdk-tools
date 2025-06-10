# Azure SDK Type Name Extractor

This tool generates a comprehensive list of reserved type names for the AZC0034 analyzer to detect duplicate type names in Azure SDK libraries.

## Purpose

The AZC0034 analyzer prevents Azure SDK libraries from defining public types that conflict with:
1. .NET platform types (String, Task, List, etc.)
2. Misused nested-only types (ServiceVersion, Enumerator at top level)

This tool maintains the `reserved-type-names.txt` file used by the analyzer for efficient binary search lookups of .NET platform types.

## Features

- **Platform Types**: Comprehensive list of 130+ .NET platform types that should be avoided
- **Automated Updates**: Can be integrated into build processes to keep the list current  
- **Sorted Output**: Generates alphabetically sorted list optimized for binary search

Note: Azure SDK types are intentionally NOT included, as different Azure libraries may legitimately reference each other's types.

## Usage

### Basic Usage
```bash
dotnet run
```
This updates the default file: `../../src/dotnet/Azure.ClientSdk.Analyzers/Azure.ClientSdk.Analyzers/reserved-type-names.txt`

### Custom Output
```bash
dotnet run -- /path/to/output/reserved-types.txt
```

### Verbose Output
```bash
dotnet run -- --verbose
```

## How It Works

1. **Platform Types**: Loads a curated list of important .NET platform types  
2. **Deduplication**: Ensures no duplicate entries
3. **Sorting**: Outputs alphabetically sorted list for efficient binary search

The focus is specifically on .NET platform types to prevent naming conflicts that would confuse developers.

## Integration

### Manual Update
Run this tool whenever the reserved type list needs updating:
```bash
cd tools/azure-sdk-type-name-extractor
dotnet run
```

### Automated Integration
Add to CI/CD pipeline to ensure the list stays current:
```yaml
- name: Update Reserved Type Names
  run: |
    cd tools/azure-sdk-type-name-extractor
    dotnet run
    git add ../../src/dotnet/Azure.ClientSdk.Analyzers/Azure.ClientSdk.Analyzers/reserved-type-names.txt
```

## Output Format

The generated file contains one type name per line, alphabetically sorted:
```
AccessToken
Action
Activator
AggregateException
...
```

This format enables:
- Efficient binary search in the analyzer
- Easy manual review and maintenance
- Simple version control diffing

## Future Enhancements

- **Full Package Scanning**: Currently uses common type patterns; could be enhanced to download and scan all Azure SDK packages
- **Versioning**: Track which package versions were scanned
- **Filtering**: Add options to exclude certain types or packages
- **Validation**: Verify type names against actual usage in Azure SDK repos