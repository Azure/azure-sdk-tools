# tsp-client repo setup

Each repository that intends to support `tsp-client` for generating and updating client libraries will need to set up an `emitter-package.json` file under the `eng/` directory at the root of the repository. Client libraries generated with this tool will be outputted based on the information in the tspconfig.yaml file of the TypeSpec specification. The service directory is specified through the `parameters.service-dir.default` parameter in the tspconfig.yaml, additionally the `package-dir` option for the specific emitter is appended to the end of the path.

See the following example of a valid tspconfig.yaml file: https://github.com/Azure/azure-rest-api-specs/blob/main/specification/contosowidgetmanager/Contoso.WidgetManager/tspconfig.yaml

Using the tspconfig.yaml linked above, by default, the client libraries will be generated in the following directory for C#: `<repo>/sdk/contosowidgetmanager/Azure.Template.Contoso/`.

### Required set up

Please note that these requirements apply on the repository where the client library is going to be generated. Repo owners should make sure to follow these requirements. Users working within a repository that already accepts this tool can refer to the [Usage](./README.md#usage) section.

- Add an emitter-package.json to the repo following this [configuration](./README.md#emitter-packagejson-required).
- Add the [TempTypeSpecFiles](./README.md#temptypespecfiles) directory to the .gitignore file for your repository.

### TempTypeSpecFiles

This tool creates a `TempTypeSpecFiles` directory when syncing a TypeSpec project to your local repository. This temporary folder will contain a copy of the TypeSpec project specified by the parameters set in the tsp-location.yaml file. If you pass the `--save-inputs` flag to the commandline tool, this directory will not be deleted. You should add a new entry in the .gitignore of your repo so that none of these files are accidentally checked in if `--save-inputs` flag is passed in.

```diff title=".gitignore" lang="sh"
+ TempTypeSpecFiles/
```

### emitter-package.json (Required)

`emitter-package.json` will be used the same as a `package.json` file. If the is no `emitter-package-lock.json` file, the tool will run `npm install` on the contents of `emitter-package.json`. This file allows each repository to pin the version of their emitter and other dependencies to be used when generating client libraries.
The file should be checked into this location `<root of repo>/eng/emitter-package.json`

Example:

```json
{
  "main": "dist/src/index.js",
  "dependencies": {
    "@azure-tools/typespec-python": "0.21.0"
  }
}
```

> NOTE: tsp compile currently requires the "main" line to be there.

> NOTE: This file replaces the package.json checked into the `azure-rest-api-spec` repository.

### emitter-package-lock.json (Optional)

`emitter-package-lock.json` will be used the same as a `package-lock.json`. The tool will run a clean npm installation before generating client libraries. This file allows consistent dependency trees and allows each repository to control their dependency installation.
The file should be checked into this location: `<root of repo>/eng/emitter-package-lock.json`

> NOTE: The tool will run `npm ci` to install dependencies, so ensure that the `emitter-package-lock.json` and `emitter-package.json` files both exist and are in sync with each other.
