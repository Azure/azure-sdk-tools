# 1.1.0 

- Enhanced rendering of `use` items to more closely match the rustdoc (docs.rs) HTML view:
    - Improved handling of `use` items and module re-exports with better tracking to prevent duplication
    - Refactored module processing with enhanced sorting and organization of child items
    - Fixed bugs and improved utility functions for better code maintainability
- Updated `ReviewLine#LineId`s to stable, meaningful identifiers rather than relying on the dynamic rustdoc ids.

# 1.0.1

Updated the parser to handle multi-line doc comments and `ReviewLine`s to improve rendering in the API View tool, due to a breaking change in the API View tool's handling of multi-line `ReviewLine`.

# 1.0.0

Marks the 1.0.0 release of rust-api-parser to support Rust SDKs in API View.

# 1.0.0-beta.1

Marks the 1.0.0-beta.1 release of rust-api-parser.