## Rust SDK APIView Exporter
This TypeScript project reads a cleaned Rustdoc JSON file from the azure-sdk-for-rust repository and generates a new Rust API view capturing the exported API surface. This project organizes the API elements into modules, functions, and other Rust constructs. and preserves the parent-child hierarchy.

### Features
- Reads a Rustdoc JSON file (docs_compact.json).
- Extracts the exported API surface.
- Filters items to include only those with public visibility.
- Preserves the parent-child hierarchy.
- Writes the exported API surface to a JSON file based on the expected APIView JSON schema.

### Prerequisites
- Node.js and npm installed. You can install Node.js from nodejs.org.

## Usage
- `npm install`
- `npm run-script build`

### Run the project:
- `node ./dist/main.js {input_file_path} {output_file_path}`
    - Example: `node ./dist/main.js /workspaces/azure-sdk-tools/tools/apiview/parsers/rust-api-parser/azure_core_compact.json /workspaces/azure-sdk-tools/tools/apiview/parsers/rust-api-parser/azure_core.api.json`
    - Or if you have the package installed, you can run `rust-genapi <input_file_path> <output_file_path>`.

### License
This project is licensed under the MIT License. See the LICENSE file for details.
