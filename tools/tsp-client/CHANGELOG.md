# Release

## 2025-11-05 - 0.30.0

- Make metadata generation for tsp-client an opt-in feature. If you want to generate `tsp-client-metadata.yaml`, set `generateMetadata: true` in `tspclientconfig.yaml`.
- Rename `tsp_client_metadata.yaml` to `tsp-client-metadata.yaml`.
- Rename `tspclientconfig.yaml` to `tsp-client-config.yaml`.

## 2025-11-04 - 0.29.1

- Updated `generate-config-files` command to preserve all existing fields in emitter-package.json when updating. Previously, only dependencies and devDependencies were retained; now all extra fields are preserved.
- Output a tsp_client_metadata.yaml file during code generation with information like tsp-client version, date created or modified, and emitter-package.json content. The metadata file will be outputted when using the following commands: `init`, `update`, `generate`. NOTE: This file can and will change format and is for tsp-client usage.

## 2025-09-29 - 0.29.0

- When running the `init` command with the `update-if-exists` flag, if there is an existing tsp-location.yaml do not add or modify the `emitterPackageJsonPath` option, unless the `emitter-package-json-path` flag is passed to the command.
- Ensure that the `output-dir` value is set to repo root when resolving `emitter-output-dir` in the `init` command.

## 2025-09-22 - 0.28.3

- Fix a bug where tsp-client was always setting the `save-inputs` option under a given emitter to `false` when the --save-inputs flag wasn't passed to the tool.

## 2025-09-16 - 0.28.2

- If `package-dir` and `emitter-output-dir` are both specified in a given tspconfig.yaml give preference to `package-dir` until the legacy behavior is officially deprecated.

## 2025-08-15 - 0.28.1

- Fix bug when using `emitter-output-dir` in tspconfig.yaml, always pass the repo root path for the `{output-dir}` variable.

## 2025-08-13 - 0.28.0

- Support `emitter-output-dir` when generating a client library. `emitter-output-dir` will be given preference over client library path parsing with the `package-dir` option under the emitter.
- Add a warning when `package-dir` is used to explain that support for this option will be deprecated in the future.

## 2025-08-05 - 0.27.0

- Support multiple emitter language repositories through a global `tspclientconfig.yaml` file checked in under `<repo root>/eng`. If this file is added to a language repository the default emitter used for a library might change based on the emitters configured in their tspconfig.yaml and the order of emitters listed in the `tspclientconfig.yaml` file.

## 2025-07-29 - 0.26.1

- Support an `--update-if-exists` flag when using `tsp-client init`. This flag will update the tsp-location.yaml file based on new inputs to the command and keep any other existing config in the file.

## 2025-07-21 - 0.26.0

- Fix bug when discovering entrypoint files to only accept `main.tsp` or `client.tsp` unless otherwise specified in tsp-location.yaml.

## 2025-07-09 - 0.25.0

- Extend dependency retention in `generate-config-files` command to include regular dependencies, not just devDependencies.
- Support empty value in `--emitter-options` option.

## 2025-07-03 - 0.24.0

- Forward manually pinned dependencies in emitter-package.json when using the `generate-config-files` command.

## 2025-06-25 - 0.23.0

- Removed `tsp-client compare` command.
- Log npm package versions after install.

## 2025-06-13 - 0.22.0

- Add support for emitter options of `object` type.

## 2025-05-01 - 0.21.0

- Update `@typespec/compiler` dependency to `"1.0.0-rc.1 || >=1.0.0 <2.0.0"`.

## 2025-04-10 - 0.20.0

- Support a `--trace` flag on the `init`, `update`, and `generate` commands to enable tracing when compiling.
- Avoid looking up the repo root when an `emitter-package-json-path` is specified in `generate-config-files` command.

## 2025-04-08 - 0.19.0

- Use repo root for emitter-package path specified in `tsp-location.yaml`.
- Fix issue in generate command where the package-lock naming did not respect the user config.
- Update `generate-config-files` and `generate-lock-file` to support `emitter-package-json-path`.

## 2025-04-07 - 0.18.0

- Specify tsp-client specific configurations under a `@azure-tools/typespec-client-generator-cli` entry in tspconfig.yaml options. This affects the `additionalDirectories` configuration. Example entry in tspconfig.yaml:

```yaml
options:
  "@azure-tools/typespec-client-generator-cli":
    "additionalDirectories":
      - "specification/contosowidgetmanager/Contoso.WidgetManager.Shared/"
```

## 2025-04-03 0.17.0

- Updated peerDependency support for `@typespec/compiler` to `^1.0.0-0`.
- Support specifying an alternate path to the emitter-package.json file:
  - For the `sync` and `update` commands: support `emitterPackageJsonPath` property in tsp-location.yaml
  - For `init`: support a `--emitter-package-json-path` flag to pass in the alternate path for emitter-package.json.
- Fixed bug in `generate-config-files` command to output `main` instead of `name` for the entry point of the emitter-package.json file.

## 2025-03-25 0.16.0

- Added `install-dependencies` command to install dependencies pinned by `emitter-package.json` and `emitter-package-lock.json` at the root of the repository.
- Support a `--skip-install` flag on the `init`, `update`, and `generate` commands to skip installing dependencies during generation.

## 2025-02-13 - 0.15.4

- Enable `debug` logs for the `convert` command

## 2025-01-17 - 0.15.3

- Add `generate-config-files` command to create `emitter-package.json` and `emitter-package-lock.json` files under the `<repo root>/eng` directory.
- Support emitters with names starting with `@typespec/http-` to generate client libraries.

## 2025-01-10 - 0.15.2

- Float `@autorest/openapi-to-typespec` version between `>=0.10.6 <1.0.0`.

## 2025-01-09 - 0.15.1

- Bumped `@autorest/openapi-to-typespec` version to `0.10.6`.

## 2025-01-07 - 0.15.0

- Support specifying an entrypoint file in tsp-location.yaml.
- Ensure client.tsp selection over main.tsp in the entrypoint file search.

## 2024-12-20 - 0.14.3

- Bumped `@autorest/openapi-to-typespec` version to `0.10.5`.

## 2024-12-03 - 0.14.2

- Bumped `@autorest/openapi-to-typespec` version to `0.10.4`.

## 2024-11-08 - 0.14.1

- Print an example `tsp compile` call when the `--debug` flag is passed to a `tsp-client` command.

## 2024-11-07 - 0.14.0

- Fix `init` command when using a local spec:
  - `commit` and `repo` are no longer required.
  - tsp-location.yaml will have default values for `commit` and `repo` that should be replaced when checking in a client library.
  - Use the local tspconfig.yaml to create directory structure. (#9261)
- Fixed `formatDiagnostic` loading. [microsoft/typespec#5007](https://github.com/microsoft/typespec/issues/5007)
- Migrated tests to `vitest`.

## 2024-10-31 - 0.13.3

- Expose `fully-compatible` flag for the `convert` command
- Bumped `@autorest/openapi-to-typespec` version to `0.10.3`.

## 2024-10-21 - 0.13.2

- Bumped `@autorest/openapi-to-typespec` version to `0.10.2`.

## 2024-10-09 - 0.13.1

- Add `@autorest/core` as a dependency, and run the package from its install folder, to guarantee the versions don't change after install.
- Bumped `@autorest/openapi-to-typespec` version to `0.10.1`.

## 2024-09-10 - 0.13.0

- Bumped `@autorest/openapi-to-typespec` version to `0.10.0`.
- Removed the dependency `@autorest/csharp`.

## 2024-08-30 - 0.12.2

- Bumped `@autorest/openapi-to-typespec` version to `0.9.1`.

## 2024-08-16 - 0.12.1

- Added `--mgmt-debug.suppress-list-exception` flag to the ARM metadata generation command.
- Bumped `@autorest/openapi-to-typespec` version to `0.9.0`.
- Format updates for additional directories.

## 2024-08-15 - 0.12.0

- Check for error diagnostics during TypeSpec compilation and exit with error if found. (#8815, #8777, #8555)

## 2024-08-13 - 0.11.2

- Fix `--version` flag. (#8814)
- Added `compare` command to compare a hand-authored Swagger to a TypeSpec-generated Swagger to understand the relevant differences between them.
- Floating `@azure-tools/typespec-autorest` dependency from `>=0.44.0 <1.0.0`.

## 2024-08-08 - 0.11.1

- Removed `compare` command.

## 2024-08-08 - 0.11.0

- Added `generate-lock-file` command, see [README](https://github.com/Azure/azure-sdk-tools/blob/main/tools/tsp-client/README.md) for more information.
- Removed the `--generate-lock-file` flag and replaced it with the command above.
- Migrated tsp-client from `node:util` to `yargs` for commandline infrastructure.
- Added `compare` command to compare a hand-authored Swagger to a TypeSpec-generated Swagger to understand the relevant differences between them.

## 2024-08-05 - 0.10.0

- Added `sort-swagger` command, see [README](https://github.com/Azure/azure-sdk-tools/blob/main/tools/tsp-client/README.md) for more information.
- Copy the package.json + package-lock.json directly under TempTypeSpecFiles/. (#8583)
- Only show compile diagnostics if the `--debug` flag is passed to the command.
- Increase minimum node version to "^18.19.0 || >=20.6.0", to ensure API import.meta.resolve() is available. (#8765)
- Increase minimum `@typespec/compiler` version to `0.58.0`. (#8766)

## 2024-07-23 - 0.9.4

- Fixed issue where one additional directory entry is treated as a string instead of an array. (#8551)

## 2024-07-15 - 0.9.3

- Add autorest and plugins as dependencies, and run the packages from their install folders, to guarantee the versions don't change after install.

## 2024-07-04 - 0.9.2

- Revert `exit(1)` on tsp compile diagnostics.

## 2024-07-02 - 0.9.1

- Fix error logging after the `compile()` call and exit if diagnostics are encountered.
- Use `formatDiagnostic()` from "@typespec/compiler" to report diagnostics after compiling.

## 2024-06-21 - 0.9.0

- Prefer the `service-dir` parameter in the emitter configurations in tspconfig.yaml if specified.

## 2024-06-07 - 0.8.1

- Normalize and clean up the directory property in tsp-location.yaml.

## 2024-05-29 - 0.8.0

- Create unique directories for sparse spec checkout.

## 2024-05-20 - 0.7.1

- Added `--no-prompt` flag to skip the output directory confirmation prompt.

## 2024-04-18 - 0.7.0

- Remove `resources.json` after converting resource manager specifications.
- Support `local-spec-repo` with `init` command.

## 2024-03-19 - 0.6.0

- Support swagger to TypeSpec conversion for ARM specifications using the `--arm` flag. Example usage: `tsp-client convert --swagger-readme <path to your readme> --arm`
- Support `--generate-lock-file` flag to generate an `emitter-package-lock.json` file in the `eng/` directory based on the `emitter-package.json`. Example usage: `tsp-client --generate-lock-file`

## 2024-02-21 - 0.5.0

- Support `emitter-package-lock.json` files.
- Use `npm ci` in case a package-lock.json file exists in the TempTypeSpecFiles directory.
- Renamed `installDependencies` function to `npmCommand`.
- `npmCommand` function takes a list of arguments and supports running various npm commands.

## 2024-02-06 - 0.4.1

- Fix tspconfig.yaml file processing when a url is passed to the `init` command.
- Support passing directory containing tspconfig.yaml for local specifications in `init` command.
- Delete sparse-spec directory if it exists when running the program.

## 2024-01-23 - 0.4.0

- Added support for initializing a project from a private repository specification.
- Added `convert` command to support swagger to TypeSpec project conversion.
- Changed `doesFileExist()` function to check local file system.
- Removed `fetch()` function.

## 2023-12-21 - 0.3.0

- Fix TypeSpec compilation issue with module-resolver.
- Use `resolveCompilerOptions` to get emitter configurations from tspconfig.yaml.
- Remove unused functions: `getEmitterOptions()`, `resolveCliOptions()`, `resolveImports()`.
- Fix `additionalDirectories` property outputted by the `init` command.
- Fixed support for local and remote specs using additional directories.
- Switched to use path helper functions from `@typespec/compiler`.

## 2023-12-8 - 0.2.0

- Use the `@typespec/compiler` module installed locally in the `TempTypeSpecFiles/` directory.

## 2023-11-14 - 0.1.1

- Add support for non-interactive sessions by using the default output directory without prompting for confirmation.

## 2023-10-20 - 0.1.0

- Initial Release
