# Rust SDK APIView Exporter

Compatibility parser for Rust APIView inputs.

## Overview

The **current** Rust APIView design lives in `Azure/azure-sdk-for-rust` under
`eng/tools/generate_api`. That tool now generates the complete APIView tree-style
`CodeFile` JSON itself and sets `ParserVersion` from the tool version
(`2.0.0` or newer). The resulting token file is uploaded to APIView as an
already-generated review token file.

This repository's TypeScript parser is now primarily a **compatibility bridge**:

- if the input already has `ParserVersion >= 2.0.0`, it passes the JSON through unchanged
- otherwise, it uses the legacy rustdoc-JSON conversion path and emits the older
  APIView `CodeFile` shape produced by this tool

## Current end-to-end flow

Generate the APIView token file in `azure-sdk-for-rust`:

```sh
cargo run --manifest-path eng/tools/Cargo.toml -p generate_api -- \
  --manifest-path sdk/core/azure_core/Cargo.toml \
  --format apiview \
  --output target/generate_api/azure_core
```

That writes `apiview.json`, whose top-level shape already matches APIView's
tree-style schema (`PackageName`, `PackageVersion`, `ParserVersion`, `Language`,
`ReviewLines`).

For manual website uploads, rename `apiview.json` to `<package>.rust.json`.

For the new design, this parser does not transform the file; it only preserves
pass-through behavior if APIView still invokes it.

## Legacy compatibility

Older Rust API review artifacts came from `eng/tools/generate_api_report` in the
Rust repo. That tool produced cleaned rustdoc JSON, not a complete APIView token
file, so this parser converted it into APIView `CodeFile` JSON.

Keep using this parser for any older input that is missing `ParserVersion` or has
`ParserVersion < 2.0.0`.

### Legacy CLI

```sh
# Development mode
ts-node src/main.ts <input_file_path> <output_file_path>

# Production mode
node ./dist/src/main.js <input_file_path> <output_file_path>

# Installed package
rust-genapi <input_file_path> <output_file_path>
```

Example:

```sh
rust-genapi ./inputs/azure_core.rust.json ./outputs/azure_core.json
```

## Legacy version alignment

The old rustdoc-based flow depended on tight alignment between:

- the nightly rustdoc JSON `format_version`
- the vendored `rustdoc-types`
- this parser's legacy conversion logic
- the old `generate_api_report` producer in `azure-sdk-for-rust`

That compatibility information is still relevant only when reprocessing older
rustdoc JSON files. For the current design, the Rust repo's `generate_api`
implementation is the source of truth and produces APIView-ready output directly.

## Local development

```sh
npm install
npm run build
```

Helpful project areas:

- `src/`: legacy converter and pass-through entry point
- `rustdoc-types/`: Rustdoc type model used by the legacy path
- `test/`: pass-through and legacy compatibility coverage

## License

This project is licensed under the MIT License. See the LICENSE file for details.