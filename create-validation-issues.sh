#!/bin/bash
# Script to create individual GitHub issues for language-specific validation tasks
# from tracking issue #13893

set -e

REPO="Azure/azure-sdk-tools"
PARENT_ISSUE="13893"
LABEL="dev inner loop"

# Function to create an issue
create_issue() {
    local title="$1"
    local body="$2"
    local assignee="$3"
    
    echo "Creating issue: $title"
    if [ -n "$assignee" ]; then
        gh issue create \
            --repo "$REPO" \
            --title "$title" \
            --body "$body" \
            --label "$LABEL" \
            --assignee "$assignee"
    else
        gh issue create \
            --repo "$REPO" \
            --title "$title" \
            --body "$body" \
            --label "$LABEL"
    fi
}

# .NET - Package Installation Check
create_issue \
    "[.NET] Implement Package Installation Check validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement package installation check validation for .NET packages to verify packages can be successfully installed before release.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies .NET package installation
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for installation failures
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "m-redding"

# Java - Verify Release Set
create_issue \
    "[Java] Implement Verify Release Set validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement release set verification for Java packages to validate the set of artifacts being released together is correct and complete.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies Java release set consistency
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for invalid release sets
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "samvaity"

# Java - Verify Artifact Versions
create_issue \
    "[Java] Implement Verify Artifact Versions validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement artifact version verification for Java packages to ensure version numbers are correct and consistent across the release set.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies Java artifact versions
- [ ] Check version consistency across related artifacts
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for version inconsistencies
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "samvaity"

# Java - Verify No Unreleased Dependencies
create_issue \
    "[Java] Implement Verify No Unreleased Dependencies validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement validation to ensure Java packages do not depend on unreleased versions of other packages.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks for unreleased dependencies
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging identifying unreleased dependencies
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "samvaity"

# Java - Spell Check
create_issue \
    "[Java] Implement Spell Check validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement spell checking for Java packages. Currently only Python is supported. The implementation should be moved from Python-specific code to the languageService base class for reuse across all languages.

## Acceptance Criteria
- [ ] Move spell check implementation from Python to languageService base class
- [ ] Implement/enable spell check for Java packages
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for spelling errors
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "samvaity"

# JavaScript - Package Installation Validation
create_issue \
    "[JavaScript] Implement Package Installation Validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement package installation validation for JavaScript packages to verify packages can be successfully installed before release.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies JavaScript package installation
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for installation failures
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "timovv"

# Python - Analyze Dependencies
create_issue \
    "[Python] Implement Analyze Dependencies validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement dependency analysis for Python packages to validate dependencies are correct, complete, and use appropriate versions.

## Acceptance Criteria
- [ ] Implement validation tool/command that analyzes Python package dependencies
- [ ] Check for dependency version conflicts
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for dependency issues
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "LibbaLawrence"

# Go - doc.go Verification
create_issue \
    "[Go] Implement doc.go Verification validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement doc.go file verification for Go packages to ensure required package documentation is present and properly formatted.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies doc.go files
- [ ] Check for required documentation sections
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for doc.go issues
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "richardpark-msft"

# Go - Licence Check
create_issue \
    "[Go] Implement Licence Check validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement license verification for Go packages to ensure proper license files and headers are present.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks Go package licenses
- [ ] Verify license file presence and content
- [ ] Check license headers in source files
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for license issues
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "richardpark-msft"

# Go - Documentation Validation
create_issue \
    "[Go] Implement Documentation Validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases.

## Description
Implement comprehensive documentation validation for Go packages to ensure documentation meets quality standards.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks Go package documentation
- [ ] Verify exported symbols are documented
- [ ] Check documentation formatting and completeness
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for documentation issues
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "richardpark-msft"

# Go - Major Version Consistency
create_issue \
    "[Go] Implement Major Version Consistency validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases. This is a release-stage check.

## Description
Implement major version consistency validation for Go packages to ensure major version numbers are consistent across module paths and import paths.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks Go major version consistency
- [ ] Verify module path version suffixes match semantic versions
- [ ] Integrate with \`azsdk_package_run_check\` workflow (release stage)
- [ ] Add appropriate error messaging for version inconsistencies
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "richardpark-msft"

# Go - No Replace Directive
create_issue \
    "[Go] Implement No Replace Directive validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases. This is a release-stage check.

## Description
Implement validation to ensure Go modules do not contain replace directives in go.mod files before release, as these should not be present in published packages.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks for replace directives in go.mod
- [ ] Integrate with \`azsdk_package_run_check\` workflow (release stage)
- [ ] Add appropriate error messaging when replace directives are found
- [ ] Document usage in tool documentation

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    "richardpark-msft"

# Cross-Language - Verify REST API Spec Location
create_issue \
    "[Cross-Language] Implement Verify REST API Spec Location validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases. This is a cross-language validation needed for all languages.

## Description
Implement validation to verify REST API spec location is correctly specified and accessible for SDK packages.

## Acceptance Criteria
- [ ] Implement validation tool/command that verifies REST API spec location
- [ ] Check spec file exists and is accessible
- [ ] Verify spec location metadata is correct
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for spec location issues
- [ ] Document usage in tool documentation
- [ ] Support all SDK languages

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    ""

# Cross-Language - Verify Links
create_issue \
    "[Cross-Language] Implement Verify Links validation" \
    "## Context
Part of the validation stage (#${PARENT_ISSUE}) for self-service SDK releases. This is a cross-language validation needed for all languages.

## Description
Implement validation to verify links in documentation and README files are valid and accessible.

## Acceptance Criteria
- [ ] Implement validation tool/command that checks documentation links
- [ ] Validate links in README, CHANGELOG, and other documentation
- [ ] Handle both internal and external links appropriately
- [ ] Integrate with \`azsdk_package_run_check\` workflow
- [ ] Add appropriate error messaging for broken links
- [ ] Document usage in tool documentation
- [ ] Support all SDK languages

## Related Issue
Tracking issue: #${PARENT_ISSUE}" \
    ""

echo "All issues created successfully!"
