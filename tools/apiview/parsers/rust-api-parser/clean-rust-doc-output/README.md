# Azure Rust API Exporter

This Rust project reads a Rustdoc JSON file and generates a new JSON file capturing the exported API surface of the given package. The project filters the items to include only those with public "visibility".

## Features

-   Reads a Rustdoc JSON file (`docs.json`).
-   Extracts the exported API surface.
-   Filters items to include only those with public(& default) visibility.
-   Writes the exported API surface to a new JSON file (`docs.api.json`).

## Prerequisites

-   Rust toolchain installed. You can install Rust from <https://rust-lang.org>.

## Usage

1. **Get the rustdoc output for the temp project**:

    ```sh
        cargo +nightly rustdoc -Z unstable-options --output-format json --package docs --all-features
    ```

2. Put the generted `docs.json` file in the `inputs` folder of this project.

3. **Run the project**:

    ```sh
    cargo run
    ```

4. **Output**:
   The exported API surface will be saved to `docs.api.json` in the root directory.
