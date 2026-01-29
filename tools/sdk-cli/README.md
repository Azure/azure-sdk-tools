# SDK CLI Tool

A command-line interface for SDK sample generation with support for multiple languages and AI-assisted workflows.

## Overview

SDK CLI provides three modes of operation:
- **CLI Mode**: Direct command-line usage for quick sample generation
- **MCP Mode**: Model Context Protocol server for VS Code and Claude integration
- **ACP Mode**: Agent Client Protocol for interactive AI-assisted workflows

## Supported Languages

- .NET/C#
- Python
- TypeScript
- JavaScript
- Java
- Go

## Installation

```bash
# Build from source
dotnet build Sdk.Tools.Cli/Sdk.Tools.Cli.csproj
```

## Usage

### CLI Mode

Generate samples directly from the command line:

```bash
# Generate a sample for an SDK package
sdk-cli package samples --language dotnet --package ./path/to/openai-dotnet

# Specify output directory
sdk-cli package samples --language python --package ./path/to/openai-python --output ./samples
```

### MCP Mode

Start the MCP server for VS Code integration:

```bash
sdk-cli mcp
```

Configure in VS Code's MCP settings:

```json
{
  "mcp.servers": {
    "sdk-cli": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/Sdk.Tools.Cli"]
    }
  }
}
```

### ACP Mode

Start interactive mode with Agent Client Protocol:

```bash
sdk-cli acp
```

This mode enables rich, interactive AI-assisted sample generation with:
- Step-by-step guidance
- Real-time feedback
- Permission-based file operations
- Plan visualization

## Example: Generating Samples for OpenAI .NET SDK

```bash
# Clone the OpenAI .NET SDK
git clone https://github.com/openai/openai-dotnet.git

# Generate samples
sdk-cli package samples --language dotnet --package ./openai-dotnet/src/OpenAI --output ./openai-samples

# Or use interactive mode for guided generation
sdk-cli acp
> generate samples for ./openai-dotnet demonstrating chat completions
```

## Architecture

### Agent Client Protocol (ACP) SDK

The `AgentClientProtocol.Sdk` provides a standalone implementation of the Agent Client Protocol:

- **JSON-RPC Layer**: Message serialization and handling
- **ND-JSON Streams**: Newline-delimited JSON transport
- **Connection Management**: Agent and host side connections
- **Schema Definitions**: Standard capability schemas

### Sample Generation

The sample generator:
1. Detects or accepts target language
2. Scans existing samples for patterns
3. Generates new samples using AI prompts
4. Writes files with proper structure

## Project Structure

```
sdk-cli/
├── AgentClientProtocol.Sdk/     # ACP SDK implementation
│   ├── Connection/              # Connection handling
│   ├── JsonRpc/                 # JSON-RPC messages
│   ├── Schema/                  # Capability schemas
│   └── Stream/                  # ND-JSON streams
├── Sdk.Tools.Cli/               # Main CLI application
│   ├── Acp/                     # ACP agent host
│   ├── Mcp/                     # MCP server
│   ├── Models/                  # Data models
│   ├── Services/                # Core services
│   │   └── Languages/           # Language-specific handlers
│   └── Tools/                   # CLI tool implementations
└── Tests/                       # Unit tests
```

## Configuration

Create `.sdk-cli.json` in your project root:

```json
{
  "defaultLanguage": "dotnet",
  "samplesDirectory": "./samples",
  "promptsDirectory": "./prompts"
}
```

## Development

### Building

```bash
# Build all projects
dotnet build

# Run tests
dotnet test
```

### Adding a New Language

1. Create language service in `Services/Languages/`
2. Add sample context in `Services/Languages/Samples/`
3. Update `LanguageDetector` patterns
4. Add prompt template in `Prompts/SampleGeneration/`

## License

MIT License - See LICENSE file for details.
