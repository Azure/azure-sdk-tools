# Rustdoc Types

This project provides tools to convert Rust models into TypeScript types using `[typeshare](https://github.com/1password/typeshare)`.

## Setup

1. **Install Rust/Cargo**:
    ```sh
    curl https://sh.rustup.rs -sSf | sh
    ```
    Restart the current shell to use `cargo`.

2. **Install Typeshare CLI**:
    ```sh
    cargo install typeshare-cli
    ```

## Usage

1. Get the latest version of `lib.rs` from [rust-lang/rust](https://github.com/rust-lang/rust/blob/fb65a3ee576feab95a632eb062f466d7a0342310/src/rustdoc-json-types/lib.rs). 
    - Make sure the `FORMAT_VERSION` matches with the desired version in your rustdoc output from the azure-sdk-for-rust repository.

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

## Files

- `lib.rs`: Copied from [rust-lang/rust](https://github.com/rust-lang/rust/blob/fb65a3ee576feab95a632eb062f466d7a0342310/src/rustdoc-json-types/lib.rs).
- `add-typeshare.rs`: Script to append `#[typeshare]` attributes in `lib.rs`.
