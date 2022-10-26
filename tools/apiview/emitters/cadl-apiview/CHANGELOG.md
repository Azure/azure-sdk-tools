# Release History

## Version 0.2.0 (10-26-2022)
Support `namespace` emitter option to filter the appropriate namespace when it cannot be automatically resolved.
  This is primarily intended for creating APIViews for libraries.
Support `version` emitter option to choose which version of a multi-versioned spec to emit. Specs with a single
  version can omit this. Multi-version specs can omit this if emitting the latest version.
No longer suppress `@doc`, `@summary`, and `@example` decorators. These can be toggled using the APIView UI.
Support rendering multi-line strings.
Change default path for generating artifacts.

## Version 0.1.1 (10-13-2022)
Support compiler-level noEmit option.
Support `output-dir` emitter option.

## Version 0.1.0 (10-5-2022)
Initial release.