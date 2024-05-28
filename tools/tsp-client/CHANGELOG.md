# Release

## Unreleased - 0.8.0

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