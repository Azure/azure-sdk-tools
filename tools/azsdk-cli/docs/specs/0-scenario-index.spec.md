# End-to-End Azure SDK Workflow Scenario Index

## Table of Contents

- [Definitions](#definitions)
- [Purpose and Scope](#purpose-and-scope)
- [Scenario Index](#scenario-index)
  - [Scenario A: Architecture Board and API Review](#scenario-a-architecture-board-and-api-review)
  - [Scenario B: Data-Plane Spec Change Through SDK Release](#scenario-b-data-plane-spec-change-through-sdk-release)
  - [Scenario C: Management-Plane Spec Change Through SDK Release](#scenario-c-management-plane-spec-change-through-sdk-release)
- [Shared Documentation Shape](#shared-documentation-shape)
- [Related Documents](#related-documents)

---

## Definitions

- **<a id="typespec-driven-change"></a>TypeSpec-Driven Change**: An SDK change introduced by changing TypeSpec source and generating or regenerating an SDK from that source.
- **<a id="non-typespec-driven-change"></a>Non-TypeSpec-Driven Change**: An SDK change not introduced by a TypeSpec change, such as a direct SDK customization, bug fix, dependency update, or change to a package without TypeSpec source.
- **<a id="architecture-board"></a>Architecture Board**: The reviewers responsible for architecture-level decisions that affect an Azure SDK's public developer experience.
- **<a id="api-review"></a>API Review**: Human evaluation of a proposed public API surface and its supporting evidence.
- **<a id="data-plane"></a>Data Plane**: Service APIs used to perform service-specific operations on data or workloads.
- **<a id="management-plane"></a>Management Plane**: ARM APIs used to create, configure, update, or delete Azure resources.
- **<a id="service-team"></a>Service Team**: The Azure service owners who initiate and drive a specification or SDK change toward release.

---

## Purpose and Scope

This document indexes the end-to-end workflow scenarios that should be
documented across Azure SDK architecture review, API review, generation, and
release. It defines only the scenario boundaries and expected branches; each
scenario's detailed steps, gates, ownership, tooling, and failure handling
belong in a separate document.

Every scenario has two first-class entry paths:

1. A [TypeSpec-driven change](#typespec-driven-change).
2. A [non-TypeSpec-driven change](#non-typespec-driven-change).

---

## Scenario Index

### Scenario A: Architecture Board and API Review

**Perspective**: [Architecture Board](#architecture-board) member or
[API reviewer](#api-review).

**Goal**: Describe review intake, evidence, decisions, feedback, approval
recording, and handoff from the architect perspective.

#### Branch A1: SDK change introduced by a TypeSpec change

1. Receive the TypeSpec-originated review request.
2. Confirm review readiness and required generated SDK evidence.
3. Review the service contract and affected SDK public surfaces.
4. Record feedback, exceptions, and the authoritative decision.
5. Hand approval status back to the spec and SDK workflows.

#### Branch A2: SDK change not introduced by a TypeSpec change

1. Receive the direct SDK review request.
2. Identify the source and scope of the public API change.
3. Confirm review readiness and language-specific evidence.
4. Review the affected SDK public surfaces.
5. Record feedback, exceptions, and the authoritative decision.
6. Hand approval status back to the SDK release workflow.

**Future scenario document**: `0-scenario-architecture-api-review.spec.md`

### Scenario B: Data-Plane Spec Change Through SDK Release

**Perspective**: [Service team](#service-team) shipping a
[data-plane](#data-plane) SDK.

**Goal**: Describe the service-team journey from change initiation through
published SDK packages and release completion.

#### Branch B1: SDK change introduced by a TypeSpec change

1. Author and validate the TypeSpec change.
2. Open and complete the specification review.
3. Generate SDKs and review the resulting public API surfaces.
4. Complete SDK pull request validation and required approvals.
5. Release packages and verify release completion.

#### Branch B2: SDK change not introduced by a TypeSpec change

1. Identify the direct SDK change and affected languages or packages.
2. Implement and validate the SDK change without a TypeSpec update.
3. Complete any required [API review](#api-review).
4. Complete SDK pull request validation and required approvals.
5. Release packages and verify release completion.

**Future scenario document**:
`0-scenario-data-plane-change-to-release.spec.md`

### Scenario C: Management-Plane Spec Change Through SDK Release

**Perspective**: [Service team](#service-team) shipping a
[management-plane](#management-plane) SDK.

**Goal**: Describe the service-team journey from change initiation through
ARM-specific validation, SDK generation or direct SDK updates, and release.

#### Branch C1: SDK change introduced by a TypeSpec change

1. Author and validate the management-plane TypeSpec change.
2. Complete specification PR checks and required ARM approvals.
3. Generate management-plane SDKs and review affected public API surfaces.
4. Complete SDK pull request validation and required approvals.
5. Release packages and verify release and KPI completion.

#### Branch C2: SDK change not introduced by a TypeSpec change

1. Identify the direct management-plane SDK change and affected languages.
2. Determine which ARM, architecture, and SDK approvals apply.
3. Implement and validate the SDK change without a TypeSpec update.
4. Complete SDK pull request validation and required approvals.
5. Release packages and verify release and KPI completion.

**Future scenario document**:
`0-scenario-management-plane-change-to-release.spec.md`

---

## Shared Documentation Shape

Each future scenario document should expand its two branches using the same
outline:

1. Actors and ownership
2. Entry conditions and triggering artifacts
3. Readiness checks
4. Review and approval gates
5. Generation or direct SDK implementation
6. SDK pull request validation
7. Release and completion signals
8. Failure, feedback, and resumption paths
9. Differences by language or release type
10. Open questions and success criteria

Shared stages should use consistent terminology while preserving the distinct
review requirements for [data-plane](#data-plane) and
[management-plane](#management-plane) workflows.

---

## Related Documents

- [Scope comparison](spec-comparison.md)
- [TypeSpec-to-SDK release workflow](typespec-to-sdk-release-workflow.spec.md)
- [Scenario 1](0-scenario-1.spec.md)
- [Scenario 2](0-scenario-2.spec.md)
