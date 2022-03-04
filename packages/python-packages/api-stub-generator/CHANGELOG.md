# Release History

## Version 0.2.10 (Unreleased)
Added support for TypedDict classes.
Added support to parse defaults from docstrings. Example
  syntax: "A value, defaults to foo."
  Also supports older, non-recommended syntax, such as:
  "A value. Default value is foo."
Added support for "CrossLanguageDefinitionId" and a --mapping-path
  parameter to supply the necessary mapping file.
Fixed issue where required keyword-only arguments displayed as optional.
Fixed issue where APIView would add a false-alarm warning to
  paged types.

## Version 0.2.9 (Unreleased)
Fixed issue where Python 3-style type hints stopped displaying
  their inner types.
Fixed issue where docstring annotations were preferred over
  Python 2-style type hints for return types. Since docstrings
  have a 2-line limit, this preference didn't make sense.
Fixed issue where too many blank lines were generated.

## Version 0.2.8 (Unreleased)
Kwargs that were previously displayed as "type = ..." will now
  be displayed as "Optional[type] = ..." to align with syntax
  from Python type hints.
Fixed issue where variadic arguments appeared a singular argument.
Fixes issue where default values were no longer displayed.

## Version 0.2.7 (Unreleased)
Updated version to regenerate all reviews using Python 3.9

## Version 0.2.6 (Unreleased)
Updated type parsing to properly handle cases in which type
  info wraps onto a second line.
Fixed issue where ivar type was not properly parsed from
  the docstring.
Fixed issue where typehint parsing was too strict and required
  spaces.

## Version 0.2.5 (Unreleased)
Fixed bug in which kwargs were duplicated in certain instances.
Fix issue where non-async methods were erroneously marked async.
Fixed but where, in some instances, return types were truncated.

## Version 0.2.2 (Unreleased)
Bug fixes in type hint parser

## Version 0.2.1 (Unreleased)
Added package name in review node

## Version 0.2.0 (Unreleased)
Added support for packages with non-azure root namespace

## Version 0.0.1 (Unreleased)
Initial version of API stub generator for Azure SDK API view tool