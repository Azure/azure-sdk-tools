# Release History

## Version 0.2.2 (Unreleased)
- Fix issue where extension members were duplicated.

## Version 0.2.1 (Unreleased)
- Fix issue where framework-generated APIViews had an
  unstable order.

## Version 0.2.0 (Unreleased)
- Switch from SwiftAST dependency to SwiftSyntax.
- Overhaul to how extensions are displayed.

## Version 0.1.4 (Unreleased)
- Allow attributes inside function signatures.

## Version 0.1.3 (Unreleased)
- Support for getter/setter blocks.
- Fixed issue where read-only properties would appear as
  read/write due to not displaying `{ get }` syntax.

## Version 0.1.2 (Unreleased)
- Fixed issue where empty extension blocks were displayed.
- Temporarily will allow duplicate IDs. This will result in
  APIView not crashing, but may result in abnormalities
  when attempting to comment on the APIView. This will be
  fixed in an upcoming version. 

## Version 0.1.1 (Unreleased)
- Fixed issue where getter/setter blocks would fail to process.
- Fixed issue where function initializers would fail to process.

## Version 0.1.0 (Unreleased)
Initial version.
