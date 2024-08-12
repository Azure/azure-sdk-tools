# Release

## 2024-08-12 - 0.11.2

- Fix `--version` flag. (#8814)

## 2024-08-08 - 0.11.1

- Removed `compare` command.

## 2024-08-08 - 0.11.0

- Added `generate-lock-file` command, see [README](https://github.com/Azure/azure-sdk-tools/blob/main/tools/tsp-client/README.md) for more information.
- Removed the `--generate-lock-file` flag and replaced it with the command above.
- Migrated tsp-client from `node:util` to `yargs` for commandline infrastructure.

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
