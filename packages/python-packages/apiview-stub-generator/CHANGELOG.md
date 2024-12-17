# Release History

## Version 0.3.14 (Unreleased)
Update for tree token style parser.

## Version 0.3.13 (2025-01-13)
Fix bug with missing optional dependencies.

## Version 0.3.12 (2024-02-27)
Add support for Cross Language Package ID.

## Version 0.3.11 (2024-1-11)
Remove ePylint dependency.

## Version 0.3.10 (2023-11-17)
Update package version in API review codefile

## Version 0.3.9 (2023-11-17)
Improve support for "CrossLanguageDefinitionId".

## Version 0.3.8 (2023-07-06)
Display `--source-url` as an actual clickable link.
Fix issue with line ids in diffs so that the entire signature doesn't turn red if something is changed. 

## Version 0.3.7 (2023-03-09)
Fix incorrect type annotation.
Update to follow best practices for accessing '__annotations__'.
Fixed issue where class decorators were not displayed.
Fixed issue where ivars appeared as cvars.
Fixed issue where type hints did not appear for datetime.datetime types.

## Version 0.3.6 (2022-10-27)
Suppressed unwanted base class methods in DPG libraries.

## Version 0.3.5 (2022-10-26)
Fixed issue where properties could be inadvertently rendered as TypedDict keys.
Fixed issue where some sync and async functions could not be distinguished from eachother in APIView, leading to improper comment placement.

## Version 0.3.4 (2022-08-11)
Fixed issue so that APIView is still generated even if pylint parsing fails.
Fixed issue where diagnostics could be duplicated on functions with the same name.
Fixed issue where `typing.NewType` aliases were not displayed in APIView.

## Version 0.3.3 (2022-08-03)
Fixed issue in module order to get consistent order

## Version 0.3.2 (2022-07-19)
Fixed issue where comments would appear incorrectly on overloaded functions.
Fixed issue where inherited overloads would not appear in APIView.

## Version 0.3.1 (2022-05-12)
Fixed issue where pylintrc file was not included in the distribution.

## Version 0.3.0 (2022-05-11)
Added support for @overloads decorators.
Added limited support for @dataclass annotation. See known issues here (https://github.com/Azure/azure-sdk-tools/issues/3161)
Added support for positional-only arguments.
Added full support for Python 2-style type hints.
Added support for `--source-url` which allows you to specify a link to the
  pull request that the APIView is generated for that will appear in the
  APIView preamble. Intended primarily for use by other automation tools.
Fixed issue where decorators with parameters would not appear in APIView.
Fixed consistency issues with how default values are displayed.
Fixed issue where types would appear wrapped in "Optional" even though
  they do not accept `None`.
Fixed issue where, in some cases, string literal default values would not appear wrapped
  in quotes.
Fixed issue where class declarations were not properly displayed if the class
  inherited from a generic type.
Changed default for retrieving type info from docstrings to annotation/type comments.
APIView will now display diagnostics only for custom pylint rule violations
  described in the `azure-sdk-for-python` repo.
Removed custom APIView diagnostic messages that existed in prior versions.
Removed the `--hide-report` option.

## Version 0.2.11 (2022-04-06)
Added __main__ to execute as module

## Version 0.2.10 (2022-03-09)
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
Fixed issue where aliased models would be displayed with their non-public,
  unaliased name. APIView will issue a diagnostic warning if `__name__` is
  not updated to match the alias.
Fixed issue where enums that correspond to the same value would be omitted.

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
