## Rust SDK APIView Exporter
This TypeScript project reads a cleaned Rustdoc JSON file from the azure-sdk-for-rust repository and generates a new Rust API view capturing the exported API surface. This project organizes the API elements into modules, functions, and other Rust constructs. and preserves the parent-child hierarchy.

### Features
- Reads a Rustdoc JSON file (docs.api.json).
- Extracts the exported API surface.
- Filters items to include only those with public visibility.
- Preserves the parent-child hierarchy.
- Writes the exported API surface to a new Rust file (docs.rs).

### Prerequisites
- Node.js and npm installed. You can install Node.js from nodejs.org.

## Usage
- `npm install`
- `npm i -g typescript ts-node`

### Run the project:
- Ensure the {package_name}_compact.json file generated is placed in the inputs directory.
- `ts-node src/main.ts --package={package_name}`
    - Example: `ts-node src/main.ts --package=azure_core`
- Output: The exported API surface will be saved to `outputs/docs.api.json` in the outputs directory.

### License
This project is licensed under the MIT License. See the LICENSE file for details.
