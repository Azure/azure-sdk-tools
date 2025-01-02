# Release History

## 0.5.0 (unreleased)

- Added `httpx` as an import flagged by C4749(networking-import-outside-azure-core-transport)
- Checker to warn against legacy typing (do-not-use-legacy-typing)
- Checker to warn against errors being raised and logged (do-not-log-raised-errors)
- Refactored test suite
- Checker to warn against importing `asyncio` directly. (do-not-import-asyncio)
- Checker to warn against logging bare `Exception`. (do-not-log-exceptions)
- Refactored and enabled checker to warn if client does not have approved name prefix. (unapproved-client-method-name-prefix)
- Checker to warn if `connection_verify` is hardcoded to a boolean value. (do-not-hardcode-connection-verify)
- Checker to warn if sync and async overloads are mixed together. (invalid-use-of-overload).


## 0.4.1 (2024-04-17)

- Bug fix for typing under TYPE_CHECKING block.

## 0.4.0 (2024-04-15)

- Checker to enforce no importing typing under TYPE_CHECKING block.

## 0.3.1 (2023-1-16)

- Docstring bug fix where paramtype was being considered for params

## 0.3.0 (2023-12-15)

- Breaking changes involved in bump to pylint 3.0 support and bug fix to `incorrect-naming-convention` checker

## 0.2.0 (2023-10-17)

- Checker to enforce docstring keywords being keyword-only in method signature.
- Fixed a bug in `no-legacy-azure-core-http-response-import` that was throwing warnings for azure-mgmt-core.

## 0.1.0 (2023-08-02)

Add two new checkers:
- Checker to warn against importing the package `six`
- Checker to warn against importing `HttpResponse` from `azure.core.pipeline.transport` 

## 0.0.9 (2023-06-26)
Fix bug with varargs in CheckDocstringParameters checker.
Updated client-paging-methods-use-list checker to include private methods.