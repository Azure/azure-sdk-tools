# Contributing to TypeSpec APIView Emitter

Thank you for contributing to the TypeSpec APIView Emitter! This guide will help you set up your environment and debug the emitter, whether you're working with a local TypeSpec file or targeting a spec in the Azure REST API specs repository.

---

## Prerequisites

1. **Install Node.js**: Ensure you have Node.js (version 16 or later) installed. You can verify your version with:
   ```bash
   node --version
   ```
2. **Install Dependencies**: Run the following command to install all required dependencies:
   ```bash
   npm install
   ```
3. **Build the Project**: Compile the TypeScript files into JavaScript:
   ```bash
   npm run build
   ```

---

## Debugging the Emitter

### Debugging with a Local TypeSpec File

1. **Set Up a Local TypeSpec File**:
   Create a sample TypeSpec file (e.g., `sample.tsp`) in your workspace. This file will serve as a test input for your emitter:
   ```tsp
   @service({ title: "Sample Service", version: "1.0" })
   namespace SampleNamespace {
       model SampleModel {
           id: string;
           name: string;
       }
   }
   ```

2. **Update the Debug Configuration**:
   Open the `.vscode/launch.json` file and ensure the following configuration exists. The `--emit` argument specifies the emitter to use during compilation:
   ```jsonc
   {
       "type": "node",
       "request": "launch",
       "name": "Debug Emitter with Local File",
       "program": "${workspaceFolder}/node_modules/@typespec/compiler/entrypoints/cli.js",
       "args": [
           "compile",
           "${workspaceFolder}/sample.tsp",
           "--output-dir=temp/output",
           "--emit=@azure-tools/typespec-apiview"
       ],
       "smartStep": true,
       "sourceMaps": true,
       "skipFiles": ["<node_internals>/**/*.js"],
       "outFiles": [
           "${workspaceFolder}/dist/**/*.js"
       ],
       "cwd": "${workspaceFolder}"
   }
   ```

3. **Start Debugging**:
   - Open the Debug panel in VS Code (`Ctrl+Shift+D`).
   - Select the `Debug Emitter with Local File` configuration.
   - Set breakpoints in your TypeScript files (e.g., `src/emitter.ts`).
   - Press `F5` to start debugging.

---

### Debugging with a Spec in the Azure REST API Specs Repository

1. **Clone the Azure REST API Specs Repository**:
   Clone the repository to your local machine. This repository contains real-world TypeSpec specifications that you can use to test your emitter:
   ```bash
   git clone https://github.com/Azure/azure-rest-api-specs.git
   ```

2. **Install Dependencies**:
   Navigate to the cloned repository and install its dependencies:
   ```bash
   cd azure-rest-api-specs
   npm install
   ```

3. **Update the Debug Configuration**:
   Open the `.vscode/launch.json` file and ensure the following configuration exists:
   ```jsonc
   {
       "type": "node",
       "request": "launch",
       "name": "Debug Emitter with Azure Spec",
       "program": "${workspaceFolder}/node_modules/@typespec/compiler/entrypoints/cli.js",
       "args": [
           "compile",
           "C:/repos/azure-rest-api-specs/specification/<TARGET_SPEC_PATH>.tsp",
           "--output-dir=temp/output",
           "--emit=${workspaceFolder}/node_modules/dist/src/index.js"
       ],
       "smartStep": true,
       "sourceMaps": true,
       "skipFiles": ["<node_internals>/**/*.js"],
       "outFiles": [
           "${workspaceFolder}/dist/**/*.js"
       ],
       "cwd": "${workspaceFolder}"
   }
   ```

   Replace `<TARGET_SPEC_PATH>` with the path to the specific spec you want to debug. For example:
   ```
   specification/ai/Face/main.tsp
   ```

4. **Start Debugging**:
   - Open the Debug panel in VS Code (`Ctrl+Shift+D`).
   - Select the `Debug Emitter with Azure Spec` configuration.
   - Set breakpoints in your TypeScript files (e.g., `src/emitter.ts`).
   - Press `F5` to start debugging.

---

## Additional Notes

- **Rebuild After Changes**: If you make changes to the TypeScript source files, rebuild the project before debugging:
  ```bash
  npm run build
  ```

  Or just use `npm run watch` to automatically rebuild on changes.

- **Debugging Tips**:
  - Use `debugger;` statements in your code to pause execution and inspect variables.
  - Ensure `sourceMaps` is enabled in your debug configuration to step through TypeScript files.
  - If breakpoints are not being hit, ensure that your `outFiles` in the debug configuration matches the location of your compiled files.

---

Thank you for contributing! If you encounter any issues, feel free to open an issue or reach out to the maintainers.