# APIViewUnitTests — Agent Guidelines

## Overview

xUnit test suite for the APIView solution. Uses **Moq** for mocking and **FluentAssertions** for assertions.

## Conventions

- One test class per production class (e.g., `ReviewManagerTests` tests `ReviewManager`).
- Test method naming: `MethodName_Scenario_ExpectedResult` or descriptive equivalents already in the codebase.
- Mock dependencies via Moq; inject into the class under test.
- Use FluentAssertions (`Should()`, `BeEquivalentTo()`, etc.) for assertions.
- Sample test data files live in `SampleTestFiles/`.
- `Common.cs` contains shared test helpers — check there before creating new ones.
- **After making changes, evaluate whether `README.md` or `CONTRIBUTING.md` in sibling projects need updates** to stay consistent with any new or changed test patterns.
- **Also evaluate whether any files in `../docs/` need updates** to reflect architectural or behavioral changes.
