# Azure SDK Error Analyzers

[![Build Status](https://dev.azure.com/azure-sdk/public/_apis/build/status/Azure.azure-sdk-tools?branchName=main)](https://dev.azure.com/azure-sdk/public/_build/latest?definitionId=1&branchName=main)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)

A comprehensive library for analyzing and automatically fixing Azure SDK code quality issues. This tool helps developers maintain consistency and follow Azure SDK guidelines by providing automated error detection and intelligent fix suggestions.

## 🚀 Features

- **🔍 Automated Error Detection** - Identifies common Azure SDK coding pattern violations
- **⚡ Intelligent Fix Generation** - Provides automated fixes for detected issues
- **🏗️ Modular Architecture** - Separate analyzers for Client, Management, and General rules
- **🔧 Provider Pattern** - Extensible design for adding custom analyzers
- **⚙️ Thread-Safe** - Concurrent provider registration and analysis
- **📊 Batch Processing** - Analyze multiple errors efficiently
- **🎯 Zero Reflection** - High-performance implementation

## 📦 Packages

| Package | Description | Version |
|---------|-------------|---------|
| `Azure.Tools.ErrorAnalyzers` | Core library with base classes and services | [![NuGet](https://img.shields.io/nuget/v/Azure.Tools.ErrorAnalyzers.svg)](https://www.nuget.org/packages/Azure.Tools.ErrorAnalyzers) |
| `Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers` | Analyzers for Azure SDK client libraries | [![NuGet](https://img.shields.io/nuget/v/Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers.svg)](https://www.nuget.org/packages/Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers) |
| `Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers` | General .NET coding rule analyzers | [![NuGet](https://img.shields.io/nuget/v/Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers.svg)](https://www.nuget.org/packages/Azure.Tools.ErrorAnalyzers.GeneralRuleAnalyzers) |
| `Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers` | Analyzers for Azure management libraries | [![NuGet](https://img.shields.io/nuget/v/Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers.svg)](https://www.nuget.org/packages/Azure.Tools.ErrorAnalyzers.ManagementRuleAnalyzers) |

## 🏁 Quick Start

### Installation

```bash
dotnet add package Azure.Tools.ErrorAnalyzers
dotnet add package Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers
```

### Basic Usage

```csharp
using Azure.Tools.ErrorAnalyzers;
using Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers;

// Register analyzer providers
ErrorAnalyzerService.RegisterProvider(new ClientAnalyzerProvider());

// Analyze a single error
var error = new RuleError("AZC0012", 
    "Type name 'Client' is too generic. Consider using a more descriptive multi-word name, such as 'ServiceClient'.");

bool canHandle = ErrorAnalyzerService.CanHandle(error.type);
if (canHandle)
{
    Fix? fix = ErrorAnalyzerService.GetFix(error);
    if (fix is RenameFix renameFix)
    {
        Console.WriteLine($"Rename '{renameFix.OriginalName}' to '{renameFix.NewName}'");
    }
}

// Batch process multiple errors
var errors = new[]
{
    new RuleError("AZC0012", "Type name 'Helper' is too generic..."),
    new RuleError("AZC0012", "Type name 'Manager' is too generic...")
};

var fixes = ErrorAnalyzerService.GetFixes(errors).ToList();
Console.WriteLine($"Generated {fixes.Count} automated fixes");
```

## 📖 Supported Error Types

### Client Library Rules (AZC series)

| Error Code | Description | Fix Type |
|------------|-------------|----------|
| **AZC0012** | Generic type names (e.g., 'Client', 'Helper') | `RenameFix` |

*More analyzers coming soon...*

### General Rules

*Coming soon - General .NET coding standards*

### Management Rules  

*Coming soon - Azure management library specific rules*

## 🏗️ Architecture

The library follows a clean, modular architecture:

```
Azure.Tools.ErrorAnalyzers/
├── Core/                     # Base classes and services
│   ├── ErrorAnalyzerService  # Main entry point
│   ├── AgentRuleAnalyzer     # Base analyzer class
│   ├── IAnalyzerProvider     # Provider interface
│   └── Fix classes          # Fix implementations
├── Client/                   # Client library analyzers
├── General/                  # General .NET analyzers  
├── Management/              # Management library analyzers
└── samples/                 # Usage examples
```

### Key Components

- **`ErrorAnalyzerService`** - Central service for error analysis and fix generation
- **`IAnalyzerProvider`** - Interface for registering analyzer collections
- **`AgentRuleAnalyzer`** - Base class for implementing rule-specific analyzers
- **`Fix`** - Base class for automated fix instructions
- **`RuleError`** - Normalized error representation

## 🔧 Integration Scenarios

### Build-Time Integration

```xml
<!-- In your .csproj file -->
<PackageReference Include="Azure.Tools.ErrorAnalyzers.ClientRuleAnalyzers" Version="1.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>analyzers</IncludeAssets>
</PackageReference>
```

### IDE Integration

```csharp
// Language service integration
public class LanguageServiceProvider
{
    static LanguageServiceProvider()
    {
        ErrorAnalyzerService.RegisterProvider(new ClientAnalyzerProvider());
        ErrorAnalyzerService.RegisterProvider(new GeneralAnalyzerProvider());
    }
}
```

### CI/CD Pipeline

```csharp
// Batch process build errors
var buildErrors = ParseBuildOutput(buildLogPath);
var analysisResults = ErrorAnalyzerService.GetFixes(buildErrors);

foreach (var fix in analysisResults)
{
    ApplyFixToCodebase(fix);
}
```

## 📊 Performance Characteristics

- **Thread-Safe**: All operations are thread-safe and can be called concurrently
- **Lazy Initialization**: Analyzers are loaded only when needed
- **Linear Complexity**: O(n) performance for analyzing n errors
- **Memory Efficient**: Uses read-only collections and minimal allocations
- **Zero Reflection**: No runtime reflection overhead

## 🧪 Testing

Run the test suite:

```bash
dotnet test
```

Run specific test categories:

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

## 📚 Samples

Explore the [samples directory](./samples/) for comprehensive usage examples:

- **[Basic Usage](./samples/basic-usage/)** - Getting started guide
- **Advanced Integration** - *Coming soon*
- **Custom Analyzers** - *Coming soon*

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details on:

- Setting up the development environment
- Creating new analyzers
- Adding support for new error types
- Testing guidelines
- Pull request process

### Adding a New Analyzer

1. **Create the analyzer class**:
```csharp
internal sealed class AZC0013RuleAnalyzer : AgentRuleAnalyzer
{
    public override bool CanFix(RuleError error) => 
        string.Equals(error.type, "AZC0013", StringComparison.OrdinalIgnoreCase);

    public override Fix? GetFix(RuleError error)
    {
        // Implementation here
    }
}
```

2. **Register in provider**:
```csharp
private static readonly IReadOnlyList<AgentRuleAnalyzer> clientAnalyzers = new AgentRuleAnalyzer[]
{
    new AZC0012RuleAnalyzer(),
    new AZC0013RuleAnalyzer(), // Add here
};
```

3. **Add comprehensive tests**

## 📋 Requirements

- **.NET 9.0** or later
- **C# 13** language features
- **Nullable reference types** enabled

## 🔗 Related Projects

- [Azure SDK for .NET](https://github.com/Azure/azure-sdk-for-net)
- [Azure SDK Design Guidelines](https://azure.github.io/azure-sdk/dotnet_introduction.html)
- [Microsoft.CodeAnalysis](https://github.com/dotnet/roslyn)

## 📜 License

This project is licensed under the [MIT License](LICENSE) - see the LICENSE file for details.

## 🚨 Security

Microsoft takes the security of our software products and services seriously. If you believe you have found a security vulnerability in this project, please report it to the [Microsoft Security Response Center (MSRC)](https://msrc.microsoft.com/).

## 📞 Support

- **Documentation**: [Azure SDK Documentation](https://docs.microsoft.com/azure/developer/)
- **Issues**: [GitHub Issues](https://github.com/Azure/azure-sdk-tools/issues)
- **Discussions**: [GitHub Discussions](https://github.com/Azure/azure-sdk-tools/discussions)
- **Stack Overflow**: Tag questions with `azure-sdk`

---

**Made with ❤️ by the Azure SDK Team**
