# Rust SDK APIView Exporter

Tool for converting Rustdoc JSON output into a structured format for Azure SDK API View project.

## Overview

This tool processes Rustdoc JSON files from the azure-sdk-for-rust repository and generates API views that capture the exported API surface. It organizes API elements into a hierarchical representation of modules, functions, traits, structs, and other Rust constructs while maintaining the parent-child relationships.

## Features

- Parses Rustdoc JSON files (e.g., `azure_core.rust.json`)
- Extracts and organizes the public API surface
- Preserves the hierarchical structure of the API
- Generates output compatible with the APIView JSON schema
- Supports both development mode (ts-node) and production mode (compiled JavaScript)

## Prerequisites

- **Rust and Cargo**: Required for the rustdoc-types processing. Install from [rust-lang.org](https://www.rust-lang.org/)
- **Node.js and npm**: Required for running the TypeScript code. Install from [nodejs.org](https://nodejs.org/)

For details about the Rust aspects of this project:
- See [rustdoc-types README](./rustdoc-types/README.md) for Rust project setup and usage
- The project uses [typeshare](https://github.com/1password/typeshare) to convert Rust models into TypeScript types

### Alpine Linux Setup

```sh
sudo apk add --update nodejs npm 
sudo npm i -g ts-node
```

## Installation

1. Clone the repository:
   ```sh
   git clone https://github.com/Azure/azure-sdk-tools.git
   cd azure-sdk-tools/tools/apiview/parsers/rust-api-parser
   ```

2. Install Node.js dependencies:
   ```sh
   npm install
   ```

3. Build the project:
   ```sh
   npm run build
   ```

## Usage

### Command Line Interface

```sh
# Development mode
ts-node src/main.ts <input_file_path> <output_file_path>

# Production mode (after building)
node ./dist/src/main.js <input_file_path> <output_file_path>

# Using the installed package
rust-genapi <input_file_path> <output_file_path>
```

### Examples

#### Processing azure_core
```sh
ts-node src/main.ts ./inputs/azure_core.rust.json ./outputs/azure_core.json
# or
node ./dist/src/main.js ./inputs/azure_core.rust.json ./outputs/azure_core.json
```

#### Processing azure_template
```sh
ts-node src/main.ts ./inputs/azure_template.rust.json ./outputs/azure_template.json
# or
node ./dist/src/main.js ./inputs/azure_template.rust.json ./outputs/azure_template.json
```

## Development

### Folder Structure
- `src/`: TypeScript source code
  - `models/`: Data models and interfaces
  - `process-items/`: Processors for different Rust items
- `rustdoc-types/`: Rust code for processing rustdoc output
- `inputs/`: Example input files
- `outputs/`: Generated output files

## License

This project is licensed under the MIT License. See the LICENSE file for details.