# Release History

## 0.5.8 (Unreleased)
- Add `do-not-store-secrets-in-test-variables` check
- Add `remove-deprecated-iscoroutinefunction` check

## 0.5.7 (2025-07-15)
- Bug fix for `do-not-use-logging-exception` checker

## 0.5.6 (2025-04-23)
- Documentation updates and fixing checkers

## 0.5.5 (2025-04-22)

- Bug fix for `do-not-log-raised-errors` checker
- `do-not-log-exceptions` renamed to `do-not-log-exceptions-if-not-debug`
- `do-not-use-logging-exception` checker added

## 0.5.4 (2025-04-16)
- Bug fix for `do-not-import-asyncio` checker

## 0.5.3 (2025-04-15)
- Updating documentation
- Bug fix for `do-not-import-asyncio` checker, was incorrectly flagging `azure.core` imports.

## 0.5.2 (2025-02-19)

- Bug fix for `do-not-log-exceptions` checker, was incorrectly flagging debug logs.

## 0.5.1 (2025-01-23)

- Bug Fix for connection_verify rule

## 0.5.0 (2025-01-06)

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