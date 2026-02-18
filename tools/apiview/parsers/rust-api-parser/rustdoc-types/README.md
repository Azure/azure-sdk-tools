# Rustdoc Types

This project provides tools to convert Rust models into TypeScript types using `[typeshare](https://github.com/1password/typeshare)`.

## Setup

1. **Install Rust/Cargo**:
    Follow https://doc.rust-lang.org/cargo/getting-started/installation.html
    Restart the terminal to use `cargo`.

2. **Install Typeshare CLI**:
    ```sh
    cargo install typeshare-cli
    ```

3. **Install Dependencies**:
    ```sh
    cargo vendor
    ```
    This will create the `vendor` directory with all required dependencies.

## Dependencies

This project uses vendored dependencies to ensure consistent rustdoc-types. The `rustdoc-types` dependency is vendored in the `vendor` directory and is configured through `.cargo/config.toml`.

### Updating rustdoc-types Version

1. Update the version in `Cargo.toml`:
    ```toml
    [dependencies]
    rustdoc-types = "0.33.0"  # Retain as baseline; add new version for migration.
    ```

2. **Vendor Dependencies**:
    ```sh
    cargo vendor
    ```
    This will update the vendored copy in `vendor/rustdoc-types/`.

3. Verify that the `FORMAT_VERSION` in `vendor/rustdoc-types/src/lib.rs` matches with the desired version in your rustdoc output from the azure-sdk-for-rust repository.

## Usage

1. The source `lib.rs` is located at `vendor/rustdoc-types/src/lib.rs`. This file contains the definitions that will be converted to TypeScript.

2. **Generate `output/lib-typeshare.rs` from `lib.rs`**:
    ```sh
    cargo run
    ```
    This will add `#[typeshare]` attributes to the `lib.rs`.

3. **Generate TypeScript Definitions**:
    ```sh
    typeshare ./output/lib-typeshare.rs --lang=typescript --output-file=output/typeshare-result.ts
    ```
    This will generate `output/typeshare-result.ts` with TypeScript types.

    Shortcomings:
    1. Does not copy forward the const `FORMAT_VERSION`
    2. `FunctionSignature#inputs` field (a tuple) is skipped, because typeshare cannot handle tuples.
    3. Generates `{type_typeshare: "TypeName", content_typeshare: {property: "value"}}` as opposed to `{TypeName: {property: "value"}}` for enums.
        - `#[serde(tag = "type_typeshare", content = "content_typeshare")]` attributes were added for Enums because typeshare forced us to.

4. **Post Processing**: 
    Handle the typeshare shortcomings with a post-processing script.
    ```sh
    ts-node post-processing.ts
    ```

## Version Compatibility

The `FORMAT_VERSION` in `vendor/rustdoc-types/src/lib.rs` is critical for compatibility with rustdoc JSON output. When updating the vendored `rustdoc-types`:

1. Check the `FORMAT_VERSION` value in the new version
2. Verify it's compatible with your rustdoc JSON output version
3. If they don't match, you may need to use a different version of `rustdoc-types`

Current `FORMAT_VERSION`: 37 (as of `rustdoc-types` 0.33.0). Unless there is a strong reason to change it, this is now considered our *base version*.
Any subsequent versions should be made compatible. Still check the input `FORMAT_VERSION` and do any migration necessary and do not warn for versions we handle e.g., v45 (as of `rustdoc-types` 0.41).

This is necessary because APIView will run the Rust API parser again on all previous JSON documents in the database, which would be incompatible.
Format v37 will, therefore, serve as our base version so that we can have something to migrate to.
This may mean that we have to create a separate `rustdoc-types.ts` for each version in the future,
including multi-version dependencies of `rustdoc-types` but hopefully we can avoid that as long as `rustdoc-types` doesn't change too much with our MSRV.

## Licensing

This project includes transformed types from the [rustdoc-json-types](https://github.com/rust-lang/rust/tree/master/src/rustdoc-json-types) crate, which is dual-licensed under MIT and Apache 2.0 licenses.

- See [NOTICE.txt](./../../../../../NOTICE.txt) for details on licensing and modifications.