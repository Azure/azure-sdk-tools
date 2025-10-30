# Release History

## 0.5.6 (Unreleased)

### Features Added
- Validate package names before adding to release plan

### Breaking Changes

### Bugs Fixed

### Other Changes

## 0.5.5 (2025-10-28)

### Features Added

- Add support for generating samples for Azure client libraries across all languages
- Add tool status in response
- Disable telemetry in debug mode.

### Bugs Fixed

- Fixed issue when linking .NET PR to release plan

## 0.5.4 (2025-10-21)

### Features

- None

### Bugs Fixed

- Fix in create release plan tool to use Active spec PR URL field in the query to resolve the failure in DevOps side.

## 0.5.3 (2025-10-17)

### Features

- Updated System.CommandLine dependency to 2.5 beta
### Bugs Fixed

- Added a language specific way to get package name for validation checks, to account for different language naming (JS uses package.json name)

## 0.5.2 (2025-10-13)

### Features

- Added new tool to update language exclusion justification and also to mark as language as excluded from release.

### Breaking Changes

- None

### Bugs Fixed

- None

## 0.5.1 (2025-10-07)

### Features

- None

### Breaking Changes

- None

### Bugs Fixed

- Create release plan tool failure
- Use existing release plan instead of failing when a release plan exists for spec PR.

## 0.5.0 (2025-09-25)

### Features

- Swap `azsdkcli` to manually versioned package, dropping auto-dev-versioning.
- Adding changelog + changelog enforcement

### Breaking Changes

- None

### Bugs Fixed

- None

## 0.4.x and Earlier

See previous releases in git history.
