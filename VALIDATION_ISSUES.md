# Validation Issues for Issue #13893

This document contains templates for creating individual GitHub issues for language-specific validation tasks from the tracking issue [#13893](https://github.com/Azure/azure-sdk-tools/issues/13893).

## How to Use

### Option 1: Using the Shell Script (Automated)
Run the provided shell script to create all issues at once:
```bash
chmod +x create-validation-issues.sh
./create-validation-issues.sh
```

### Option 2: Manual Creation
Use the templates below to create issues manually through the GitHub web interface.

---

## .NET Issues

### [.NET] Implement Package Installation Check validation

**Assignee:** @m-redding  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement package installation check validation for .NET packages to verify packages can be successfully installed before release.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies .NET package installation
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for installation failures
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

---

## Java Issues

### [Java] Implement Verify Release Set validation

**Assignee:** @samvaity  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement release set verification for Java packages to validate the set of artifacts being released together is correct and complete.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies Java release set consistency
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for invalid release sets
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

### [Java] Implement Verify Artifact Versions validation

**Assignee:** @samvaity  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement artifact version verification for Java packages to ensure version numbers are correct and consistent across the release set.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies Java artifact versions
- [ ] Check version consistency across related artifacts
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for version inconsistencies
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

### [Java] Implement Verify No Unreleased Dependencies validation

**Assignee:** @samvaity  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement validation to ensure Java packages do not depend on unreleased versions of other packages.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks for unreleased dependencies
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging identifying unreleased dependencies
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

### [Java] Implement Spell Check validation

**Assignee:** @samvaity  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement spell checking for Java packages. Currently only Python is supported. The implementation should be moved from Python-specific code to the languageService base class for reuse across all languages.

## Acceptance Criteria
- [ ] Move spell check implementation from Python to languageService base class
- [ ] Implement/enable spell check for Java packages
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for spelling errors
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

---

## JavaScript Issues

### [JavaScript] Implement Package Installation Validation

**Assignee:** @timovv  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement package installation validation for JavaScript packages to verify packages can be successfully installed before release.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies JavaScript package installation
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for installation failures
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

---

## Python Issues

### [Python] Implement Analyze Dependencies validation

**Assignee:** @LibbaLawrence  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement dependency analysis for Python packages to validate dependencies are correct, complete, and use appropriate versions.

## Acceptance Criteria
- [ ] Implement validation tool/command that analyzes Python package dependencies
- [ ] Check for dependency version conflicts
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for dependency issues
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

---

## Go Issues

### [Go] Implement doc.go Verification validation

**Assignee:** @richardpark-msft  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement doc.go file verification for Go packages to ensure required package documentation is present and properly formatted.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies doc.go files
- [ ] Check for required documentation sections
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for doc.go issues
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

### [Go] Implement Licence Check validation

**Assignee:** @richardpark-msft  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement license verification for Go packages to ensure proper license files and headers are present.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks Go package licenses
- [ ] Verify license file presence and content
- [ ] Check license headers in source files
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for license issues
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

### [Go] Implement Documentation Validation

**Assignee:** @richardpark-msft  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases.

## Description
Implement comprehensive documentation validation for Go packages to ensure documentation meets quality standards.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks Go package documentation
- [ ] Verify exported symbols are documented
- [ ] Check documentation formatting and completeness
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for documentation issues
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

### [Go] Implement Major Version Consistency validation

**Assignee:** @richardpark-msft  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases. This is a release-stage check.

## Description
Implement major version consistency validation for Go packages to ensure major version numbers are consistent across module paths and import paths.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks Go major version consistency
- [ ] Verify module path version suffixes match semantic versions
- [ ] Integrate with `azsdk_package_run_check` workflow (release stage)
- [ ] Add appropriate error messaging for version inconsistencies
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

### [Go] Implement No Replace Directive validation

**Assignee:** @richardpark-msft  
**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases. This is a release-stage check.

## Description
Implement validation to ensure Go modules do not contain replace directives in go.mod files before release, as these should not be present in published packages.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks for replace directives in go.mod
- [ ] Integrate with `azsdk_package_run_check` workflow (release stage)
- [ ] Add appropriate error messaging when replace directives are found
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #13893
```

---

## Cross-Language Issues

### [Cross-Language] Implement Verify REST API Spec Location validation

**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases. This is a cross-language validation needed for all languages.

## Description
Implement validation to verify REST API spec location is correctly specified and accessible for SDK packages.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies REST API spec location
- [ ] Check spec file exists and is accessible
- [ ] Verify spec location metadata is correct
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for spec location issues
- [ ] Document usage in tool documentation
- [ ] Support all SDK languages

## Related Issue
Tracking issue: #13893
```

### [Cross-Language] Implement Verify Links validation

**Labels:** dev inner loop

**Description:**
```markdown
## Context
Part of the validation stage (#13893) for self-service SDK releases. This is a cross-language validation needed for all languages.

## Description
Implement validation to verify links in documentation and README files are valid and accessible.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks documentation links
- [ ] Validate links in README, CHANGELOG, and other documentation
- [ ] Handle both internal and external links appropriately
- [ ] Integrate with `azsdk_package_run_check` workflow
- [ ] Add appropriate error messaging for broken links
- [ ] Document usage in tool documentation
- [ ] Support all SDK languages

## Related Issue
Tracking issue: #13893
```

---

## Summary

**Total Issues to Create:** 14

- .NET: 1 issue
- Java: 4 issues
- JavaScript: 1 issue
- Python: 1 issue
- Go: 5 issues
- Cross-Language: 2 issues

**Note:** API View validation already has a dedicated issue (#13813) and is not included in this list.
