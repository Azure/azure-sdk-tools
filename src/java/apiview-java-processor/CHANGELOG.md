<!-- Copyright (c) Microsoft Corporation. All rights reserved.
     Licensed under the MIT License. -->

# Release History

## 1.34.0 (Unreleased)

### Features Added

- Added direct Markdown output with `--renderer=markdown`; JSON remains the default.
- Added in-process sources JAR processing for the Maven plugin.

### Bugs Fixed

- Stop reading diagnostic JSON at its object boundary.
- Report invalid builder parameter shapes as diagnostics instead of aborting API review generation.
- Handle malformed AMQP transport parameters without aborting generation.
- Select embedded Maven metadata by project coordinates for custom sources JAR names.
- Isolate type spacing between analyser instances.
- Fail on source JAR processing errors.
- Recognize explicit builder protocols and check required methods independently for each builder.
- Finish and close compressed JSON output so it can be decompressed successfully.
- Detect prohibited packages in fully qualified public API types.

### Other Changes

- Imported processor updates and regression tests from Azure/azure-sdk-for-java#51226.
- Added source license headers.
