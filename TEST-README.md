# Testing the Branch Naming Disambiguation Feature

This directory contains test scripts to validate the branch naming logic in `archetype-typespec-emitter.yml`.

## Overview

The branch naming logic was modified to include disambiguation when multiple TypeSpec emitters exist for the same language. The `EmitterPackagePath` parameter is used to create unique branch names.

## Running the Tests

### PowerShell Test Script

Run the comprehensive test suite:

```bash
pwsh test-branch-naming.ps1
```

Run with verbose output:

```bash
pwsh test-branch-naming.ps1 -Verbose
```

### Test Coverage

The test script validates:

1. **Backward Compatibility**: No emitter path provided (behaves as before)
2. **Simple Emitter**: Basic emitter package naming
3. **Multiple Emitters**: Different emitter packages for the same language
4. **Build Types**: PR builds, scheduled builds, and regular builds
5. **Special Characters**: Proper sanitization of file names
6. **Complex Paths**: Nested directory structures

### Expected Results

All tests should pass, demonstrating:

- Branch names are unique for different emitters
- Special characters are properly sanitized
- Backward compatibility is maintained
- All build scenarios work correctly

## Example Output

```
🔍 Test: Java emitter disambiguation
  Generated: 'validate-typespec-20240101.3-java-emitter-package'
  Expected:  'validate-typespec-20240101.3-java-emitter-package'
  ✅ PASS
```

## Integration Testing

To test in a real pipeline scenario:

1. Create emitter packages with different names (e.g., `java-emitter.json`, `python-emitter.json`)
2. Run the pipeline with different `EmitterPackagePath` parameters
3. Verify that different branch names are generated for each emitter

## Validation

The test script copies the exact PowerShell logic from `archetype-typespec-emitter.yml` to ensure accuracy.