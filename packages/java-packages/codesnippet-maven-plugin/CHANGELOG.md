# Release History

## 1.0.0-beta.8 (2022-08-18)

### Features Added

- Added verbose exception indicating mismatch BEGIN and END codesnippet aliases to aid in troubleshooting snippet injection failures.

### Other Changes

- General performance improvements.

## 1.0.0-beta.7 (2022-04-13)

### Bugs Fixed

- Removed the need to have spaces after the Javadoc and README codesnippet identifiers.

### Other Changes

- Updated special character replacements to use `StringBuilder` instead of `Pattern`, improving performance of 
  codesnippet injection.

## 1.0.0-beta.6 (2022-01-22)

### Features Added

- Added `additionalCodesnippets` to enable retrieving codesnippets to inject from more than one directory. It's now 
  possible to retrieve codesnippets from multiple directories without needing to use a common root, which helps prevent
  parsing unexpected codesnippets.
- Added `additionalReadmes` to enable injecting codesnippets into READMEs in different directories. It's now possible to
  configure the plugin to manage external READMEs without needing to use a common root, which helps prevent injecting
  codesnippets into unexpected READMEs.

### Breaking Changes

- Removed `protected` methods for accessing Mojo configurations.

### Bugs Fixed

- Fixed a bug where indented README code fences wouldn't be injected with proper indentation, breaking formatting for 
  READMEs on sites such as GitHub.

## 1.0.0-beta.5 (2022-01-12)

### Bugs Fixed

- Fixed a bug where no source or README files for injection would cause the plugin to throw an exception. Now, if either
  is empty injection is skipped and if both are empty the plugin is a no-op.

## 1.0.0-beta.4 (2022-01-07)

### Features Added

- Added `readmeGlob` and `readmeRootDirectory` to support injecting into multiple READMEs.

### Breaking Changes

- Removed `readmePath` configuration.

## 1.0.0-beta.3 (2021-11-24)

### Features Added

- Added configuration to not fail on codesnippet errors.
- Added configuration for maximum codesnippet line length checking.

### Breaking Changes

- Changes the Mojo parameter names to be prefixed with `codesnippet.`.

### Bugs Fixed

- Fixed a bug where codesnippets would be injected into non-codesnippet Java code fences in READMEs.

## 1.0.0-beta.2 (2021-10-05)

### Bugs Fixed

- Fixed a bug where non-codesnippet code fences in READMEs would be removed. Non-codesnippet code fences are now ignored.

## 1.0.0-beta.1 (2021-09-28)

### Features Added

- Initial release of `codesnippet-maven-plugin`.