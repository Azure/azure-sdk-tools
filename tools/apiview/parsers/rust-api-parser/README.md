## Rust SDK APIView Exporter

This Rust project reads a cleaned Rustdoc JSON file from the azure-sdk-for-rust repository and generates a new Rust API view capturing the exported API surface. This project organizes the API elements into modules, functions, and other Rust constructs, preserving the parent-child hierarchy.

### Features
- Reads a Rustdoc JSON file (`docs.rust.json`).
- Extracts the exported API surface.
- Filters items to include only those with public visibility.
- Preserves the parent-child hierarchy.
- Writes the exported API surface to a JSON file based on the expected APIView JSON schema.

### Prerequisites
- Rust and Cargo installed. You can install Rust from [rust-lang.org](https://www.rust-lang.org/).
- Node.js and npm installed. You can install Node.js from [nodejs.org](https://nodejs.org/).
- Alpine Linux
    ```sh
    sudo apk add --update nodejs npm && sudo npm i -g ts-node
    ```

### Usage
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

4. Run the project:
    ```sh
    node ./dist/src/main.js {input_file_path} {output_file_path}
    ```
    - Example: 
      ```sh
      ts-node src/main.ts /workspaces/azure-sdk-tools/tools/apiview/parsers/rust-api-parser/inputs/azure_core.rust.json /workspaces/azure-sdk-tools/tools/apiview/parsers/rust-api-parser/outputs/azure_core.json
      
      node ./dist/src/main.js /workspaces/azure-sdk-tools/tools/apiview/parsers/rust-api-parser/inputs/azure_core.rust.json /workspaces/azure-sdk-tools/tools/apiview/parsers/rust-api-parser/outputs/azure_core.json

      node ./dist/src/main.js /workspaces/azure-sdk-tools/tools/apiview/parsers/rust-api-parser/inputs/azure_template.rust.json /workspaces/azure-sdk-tools/tools/apiview/parsers/rust-api-parser/outputs/azure_template.json
      ```
    - Or if you have the package installed, you can run:
      ```sh
      rust-genapi <input_file_path> <output_file_path>
      ```

### License
This project is licensed under the MIT License. See the LICENSE file for details.