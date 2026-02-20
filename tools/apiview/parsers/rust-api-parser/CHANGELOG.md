# 1.3.0

- Reverted changes made in 1.2.0 because they are not compatible with previous input.
  We will instead normalize the input in the preprocessor that runs in the Azure/azure-sdk-for-rust repo.

# 1.2.0

- Updated to `rustdoc-types` 0.41.0 to coincide with Azure SDK for Rust MSRV update to 1.88 (`nightly-2025-05-09`).
  See [Azure/azure-sdk-for-rust#3692](https://github.com/Azure/azure-sdk-for-rust/issues/3692) for details.

# 1.1.1

- Fixed duplicate line ID generation that was causing Copilot review failures (#11860):
    - Enhanced line ID sanitization to preserve semantic distinctions (e.g., `&` → `ref_`, `<` → `of_`)
    - Improved external re-export handling to prevent path-based collisions
    - Added defensive duplicate detection with automatic counter-based fixing

# 1.1.0 

- Enhanced rendering of `use` items to more closely match the rustdoc (docs.rs) HTML view:
    - Improved handling of `use` items and module re-exports with better tracking to prevent duplication
    - Refactored module processing with enhanced sorting and organization of child items
    - Fixed bugs and improved utility functions for better code maintainability
- Updated `ReviewLine#LineId`s to stable, meaningful identifiers rather than relying on the dynamic rustdoc ids.
- Updated `RenderClasses` and `NavigationDisplayName`s for various elements to improve rendering experience. 

# 1.0.1

Updated the parser to handle multi-line doc comments and `ReviewLine`s to improve rendering in the API View tool, due to a breaking change in the API View tool's handling of multi-line `ReviewLine`.

# 1.0.0

Marks the 1.0.0 release of rust-api-parser to support Rust SDKs in API View.

# 1.0.0-beta.1

Marks the 1.0.0-beta.1 release of rust-api-parser.
