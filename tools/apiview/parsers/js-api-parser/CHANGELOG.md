# 2.0.11

## New Features

- Add dedicated token generator for type aliases
- Add dedicated token generators for constructors and enum members
- Add namespace token generator
- Add call signature, construct signature, and index signature token generators

## Bug Fixes

- Fix PropertySignature and TypeAlias type navigation — add NavigateToId support for type references using a hybrid
reference map approach
- Fix extra colon rendering in variable declarations without type annotations
- Fix destructured parameter double-rendering in constructor, method, function, and callable signature generators
- Fix package version parsing

## Breaking Changes

- Remove fallback of the old token parser generator

## Other Changes

- Bump minimatch, flatted, ajv, picomatch
- Release documentation for API view parser
- Code refactoring 

# 2.0.10

- Fix the path in bin/ts-genapi.cjs
- add `typescript` to runtime dependency

# 2.0.9

- Fix version in the code to point to the current parser version

# 2.0.8

- Code refactoring of token generators for properties and property signatures

# 2.0.7

- Add support for cross-language IDs to correlate APIs across different languages
- Code refactoring of token generators for enums, classes, functions, interfaces and methods

# 2.0.6

- support `@deprecated` tag

# 2.0.5

- skip diff for @azure\* and tslib dependencies
- Support re-exported enums

# 2.0.4

- update models from latest CodeFile schemas

# 2.0.3

- add `SkipDiff: true` for dependency header line
- set related line id for pre-release tags
- add `export const` before constants
- fix leading whitespace issue around punctuation and keyword
- fix type reference issue in multi-line code
- fix contextual keyword in property/method signature
- add "class" renderClass to type parameters
- hide enum members from navigation list
- replace `export declare enum` with `export enum`
- group api items instead of sorting them alphabetically

# 2.0.2

- fix issue where `type` is not treated as keyword.

# 2.0.1

- fix incorrect token type for members of types.

# 2.0.0

- Migrate to tree token.

# 1.0.8
