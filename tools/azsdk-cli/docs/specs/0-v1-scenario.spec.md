# Spec: V1 scenario

## Overview

The V1 scenario defines the end-to-end workflow for generating and releasing preview SDKs across all five languages (.NET, Java, JavaScript, Python, Go) using the Health Deidentification service as the test case.

**Service**: Health Deidentification
- [Health Deidentification Data Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/ded7abde9c48ba84df36b53dfcaef48a2c134097/specification/healthdataaiservices/HealthDataAIServices.DeidServices)  
- [Health Deidentification MGMT Plane Spec](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/healthdataaiservices/HealthDataAIServices.Management)
**Mode**: Works in both agent and CLI modes  
**Goal**: Prove complete SDK local workflow from setup → generate → validate until release

---

## Why V1 Scenario Matters

Without a concrete end-to-end scenario, we risk building tools in isolation that don't integrate well. V1 provides:

- Clear definition of successful completion for all teams
- Repeatable test case for validation
- Scope boundaries to prevent feature creep
- Foundation for cross-language consistency

---

## Context & Assumptions

### Environment

- Windows machine with freshly cloned repos (specs + all 5 language repos)
- Existing SDKs already present in language repos
- Test resources already provisioned
- Test recordings already exist (playback mode only)
- TypeSpec modifications are local only (single repo)

### In Scope for V1

- **All five languages** (.NET, Java, JavaScript, Python, Go) - no exceptions, all must pass
- **Preview release** (not first preview, no architect review required)
- **TypeSpec-based generation** from Health Deidentification service - creating non-compatible version that ignores existing code customizations
- **With or without client.tsp** - handles both scenarios
- **Playback testing** using existing test recordings
- **Both data plane and management plane** APIs
- **Agent and CLI modes** - both must work
- **Existing changelog generation** - no changes to current process

### Out of scope for V1

- Working with or creating new SDK code customizations
- Creating new client.tsp customizations
- Breaking changes
- Live test execution
- Test resource management
- Linux/macOS support
- First preview releases
- GA releases

*Note: When testing this scenario, we will not be releasing the versions of the Health Deidentificiation libraries we create. When work on this scenario is completed, services teams should be able to run it all the way to release.*

---

## Workflow

```text
1. Environment Setup → verify-setup
   └─ Check all requirements for all 5 languages

2. Generating → generate-sdk
   └─ Generate SDK code, tests, samples from TypeSpec
   └─ Validate: build, test (playback mode), samples

3. Update Package/Docs/Metadata → update-package
   └─ Update versions, changelogs, READMEs, metadata files
   └─ Validate: versions, READMEs, changelogs

4. Validating → run-pr-checks
   └─ Run all PR CI checks locally to ensure green PR

5. Releasing (Outer Loop) → create-release-plan, create-prs, validate-release, release, close-plan
   └─ Create release plan → create all PRs → validate readiness → release all → confirm & close
   └─ All 5 languages released successfully
```

---

## Stage Details

### 1. Environment Setup
**Tool**: `verify-setup` ([#12287](https://github.com/Azure/azure-sdk-tools/issues/12287))  
**Action**: Check all requirements upfront for all languages  
**Success**: All tools/SDKs installed, user knows what's missing

### 2. Generating

**Tool**: `generate-sdk` ([#11403](https://github.com/Azure/azure-sdk-tools/issues/11403))  
**Action**: Generate SDK code, tests, samples from TypeSpec for Health Deidentification  
**Success**: Clean generation for all 5 languages  
**Validation**: `build-sdk`, `run-tests` (playback mode), `validate-samples` - ensure generated code compiles, tests pass, and samples are valid

### 3. Update Package/Docs/Metadata

**Tool**: `update-package` ([#11827](https://github.com/Azure/azure-sdk-tools/issues/11827))  
**Action**: Update versions, changelogs, READMEs, language-specific files (pom.xml, _meta.json, etc.)  
**Success**: All metadata correctly updated for preview release  
**Validation**: Validate versions, READMEs, changelogs are correctly formatted and updated

### 4. Validating

**Tools**: `run-pr-checks` ([#11431](https://github.com/orgs/Azure/projects/865/views/4?pane=issue&itemId=122229127))  
**Action**: Run all PR CI checks locally before creating PRs  
**Success**: All checks pass for all languages - PR will be green

### 5. Releasing (Outer Loop)

**Tools**: Release plan creation, PR creation, readiness validation, release execution, plan closure  
**Action**: Complete release workflow:
1. Create release plan for all 5 languages
2. Create all PRs (one per language)
3. Validate release readiness
4. Execute release for all languages
5. Confirm all languages released and close release plan

**Success**: All 5 languages released successfully, release plan closed

---

## Success Criteria

V1 is complete when:
- ✅ Complete workflow executes successfully for all 5 languages
- ✅ Works in both agent and CLI modes
- ✅ Documentation exists for both modes
- ✅ Repeatable with consistent results

---

## Open Questions

1. **Cross-language failures**: If one language fails validation, block all or continue with others? → _Proposal: Block all for V1_
2. **Partial release success**: If 4/5 languages release successfully, what happens? → _Proposal: All must succeed in validation before releasing_
3. **Rollback**: Include automated rollback or rely on Git? → _Proposal: Git-based rollback for V1_
4. **Real-world validation**: How to ensure scenario isn't too simplified? → _Proposal: Pilot with 2-3 service teams_

---

## Related Links

- [Verify Setup - #12287](https://github.com/Azure/azure-sdk-tools/issues/12287)
- [Generate SDK - #11403](https://github.com/Azure/azure-sdk-tools/issues/11403)
- [Package Metadata Update - #11827](https://github.com/Azure/azure-sdk-tools/issues/11827)
- [Build SDK](https://github.com/orgs/Azure/projects/865/views/4?pane=issue&itemId=122043733)
- [Run PR Checks - #11431](https://github.com/orgs/Azure/projects/865/views/4?pane=issue&itemId=122229127)
- [DevEx Inner Loop Project](https://github.com/orgs/Azure/projects/865/views/4)
