# Release

## Unreleased - 0.3.0

- Fix TypeSpec compilation issue with module-resolver.
- Use `resolveCompilerOptions` to get emitter configurations from tspconfig.yaml.
- Remove unused functions: `getEmitterOptions()`, `resolveCliOptions()`, `resolveImports()`.

## 2023-12-8 - 0.2.0

- Use the `@typespec/compiler` module installed locally in the `TempTypeSpecFiles/` directory.

## 2023-11-14 - 0.1.1

- Add support for non-interactive sessions by using the default output directory without prompting for confirmation.

## 2023-10-20 - 0.1.0

- Initial Release