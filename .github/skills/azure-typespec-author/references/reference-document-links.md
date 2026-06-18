# Reference Document Links

Curated catalog of authoritative TypeSpec Azure documentation, **categorized by authoring case**. The cases are derived from the structure of the [TypeSpec Azure documentation](https://azure.github.io/typespec-azure/docs/intro/) (`docs/howtos/`, `docs/getstarted/`) and, for core language concepts (models, enums, paging), the underlying [TypeSpec core documentation](https://typespec.io/docs) that the Azure libraries build on.

For each case, Step 3 (`authoring-plan.md`) selects the listed URLs as input to [agentic search](agentic-search.md). If no case matches the user's request, fall back to the **General References** section plus any URLs whose title matches request keywords.

---

## General References (always available)

- [Introduction](https://azure.github.io/typespec-azure/docs/intro/): Overview of TypeSpec Azure libraries, emitters, and intended artifacts (OpenAPI 2/3).
- [Installation](https://azure.github.io/typespec-azure/docs/getstarted/installation/): Install TypeSpec compiler and Azure libraries.
- [Create a project](https://azure.github.io/typespec-azure/docs/getstarted/createproject/): Bootstrap a new TypeSpec project.
- [TypeSpec language basics](https://typespec.io/docs/language-basics/overview/): Core language primitives (namespaces, models, operations, decorators) used by every `.tsp` file.

---

## Case 1 — Add Resource Type (ARM)

> Define a new ARM resource (tracked or proxy) with its standard operations.

- [Resource type](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/): How to declare ARM resource types (`TrackedResource`, `ProxyResource`, extension/singleton variants), parent/child hierarchies, and naming.
- [Resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): Standard CRUD interfaces (`ArmResourceCreateOrReplaceAsync`, `ArmResourceRead`, `ArmCustomPatch`, `ArmResourceDeleteAsync`, list-by-parent / list-by-subscription).
- [ARM rules](https://azure.github.io/typespec-azure/docs/howtos/arm/arm-rules/): Mandatory linter/RPC rules that new resources must satisfy.
- [RPC guidelines coverage](https://azure.github.io/typespec-azure/docs/howtos/arm/rpc-guidelines-coverage/): Mapping of TypeSpec ARM patterns to ARM RPC guideline requirements.
- [Getting started — ARM step 02 (Defining the Resources)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step02/) and [step 03 (Defining Child Resource Types)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step03/): End-to-end walkthroughs of adding tracked/proxy and child resources.

---

## Case 2 — Add Resource Operations (ARM)

> Add CRUD, list, or custom actions to an existing ARM resource.

- [Resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): All built-in ARM operation templates and how to compose them.
- [Long-running operations (ARM)](https://azure.github.io/typespec-azure/docs/howtos/arm/long-running-operations/): Async PUT/DELETE/POST patterns, `ArmCombinedLroHeaders`, `LroHeaders = ArmCombinedLroHeaders<FinalResult = ...>`.
- [ARM rules](https://azure.github.io/typespec-azure/docs/howtos/arm/arm-rules/): Linter rules covering operation shape, response codes, and pagination.
- [RPC guidelines coverage](https://azure.github.io/typespec-azure/docs/howtos/arm/rpc-guidelines-coverage/): Required vs optional operations per resource category.
- [Getting started — ARM step 05 (Defining Custom Actions)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step05/): Walkthrough of adding custom POST actions to a resource.

---

## Case 3 — API Versioning

> Add, bump, or promote an API version (preview ↔ stable), **or** add/update code (resources, operations, models, properties, enum members, defaults, optionality) scoped to a specific existing version using versioning decorators.

- [Versioning overview](https://azure.github.io/typespec-azure/docs/howtos/versioning/01-about-versioning/): Concepts, decorators (`@added`, `@removed`, `@renamedFrom`, `@madeOptional`, `@madeRequired`, `@typeChangedFrom`), and the `Versions` enum.
- [preview → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/02-preview-after-preview/): Add a new preview version after an existing preview.
- [preview → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/): Promote a preview to stable.
- [stable → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/04-preview-after-stable/): Add a preview after a stable version.
- [stable → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/05-stable-after-stable/): Add a new stable after an existing stable version.
- [Evolving APIs](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/): Add, remove, or modify resources, operations, and properties across versions.
- [ARM versioning](https://azure.github.io/typespec-azure/docs/howtos/arm/versioning/): ARM-specific versioning guidance and common-types interaction.
- [Versioning (getstarted)](https://azure.github.io/typespec-azure/docs/getstarted/versioning/): Introduction to versioning in TypeSpec Azure projects.
- [Uncommon: Converting specs](https://azure.github.io/typespec-azure/docs/howtos/versioning/uncommon-scenarios/01-converting-specs/): Versioning when converting from Swagger/OpenAPI.
- [Uncommon: Perpetual preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/uncommon-scenarios/02-perpetual-preview/): Long-lived preview-only services.

---

## Case 4 — Long-Running Operations (LRO)

> Author async operations for ARM or data-plane services.

- [Long-running operations (ARM)](https://azure.github.io/typespec-azure/docs/howtos/arm/long-running-operations/): ARM-specific LRO templates and headers (`ArmCombinedLroHeaders`, `ArmLroLocationHeader`, final-state-via).
- [Long-running operations (Azure Core)](https://azure.github.io/typespec-azure/docs/howtos/azure-core/long-running-operations/): Data-plane LRO patterns using `@azure-tools/typespec-azure-core` (`LongRunningRpcOperation`, `Azure.Core.Foundations` poll/status models).
- [Getting started — Azure Core step 05 (Defining long-running resource operations)](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step05/): Tutorial walkthrough of an LRO for a data-plane resource.
- [Getting started — ARM step 05 (Defining long-running resource operations)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step05/): Tutorial walkthrough of an async ARM operation.

---

## Case 5 — Paging

> Model paged (list) operations and custom pagination for ARM or data-plane services.

- [Pagination (TypeSpec core)](https://typespec.io/docs/standard-library/pagination/): Core paging decorators (`@list`, `@pageItems`, `@nextLink`, `@continuationToken`, `@offset`, `@pageSize`) used to describe paged responses.
- [Resource operations (ARM)](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): ARM list templates (`ArmResourceListByParent`, `ArmListBySubscription`, `ArmResourceListAtScope`) that emit standard `value` + `nextLink` paged envelopes.
- [Getting started — Azure Core step 04 (Defining standard resource operations)](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step04/): Data-plane list/paged operations via `Azure.Core` (`ResourceList`, `Azure.Core.Page`).
- [Azure Core library reference](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/): Reference for `Azure.Core` paging types and templates.

---

## Case 6 — Models and Enums

> Define or modify models, properties, enums, and unions, including their constraints, formats, and visibility. For applying decorators, see Case 7.

- [Models (TypeSpec core)](https://typespec.io/docs/language-basics/models/): Declare models, properties, optionality (`?`), spread, inheritance (`extends`), composition (`is`), and `Record`/array types.
- [Enums (TypeSpec core)](https://typespec.io/docs/language-basics/enums/): Define fixed enums and member values.
- [Unions (TypeSpec core)](https://typespec.io/docs/language-basics/unions/): Define named/anonymous unions; the Azure pattern for extensible (open) enums.
- [Extensible enum troubleshooting](https://azure.github.io/typespec-azure/docs/troubleshoot/enum-not-extensible/): Fix the `enum-not-extensible` diagnostic by switching a fixed `enum` to an extensible (union) enum.
- [Type relations & constraints](https://typespec.io/docs/language-basics/type-relations/): Scalars, constraints, and how property types validate.
- [Getting started — Azure Core step 03 (Defining your first resource)](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step03/): Modeling a resource and its properties in a data-plane service.
- [Getting started — ARM step 02 (Defining the Resources)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step02/): Modeling ARM resource properties and the `properties` envelope.

---

## Case 7 — Decorators

> Apply decorators to models, properties, operations, and members — inline or via `@@`-augment — including constraint/format, visibility, and ARM-specific decorators.

- [Decorators (TypeSpec core)](https://typespec.io/docs/language-basics/decorators/): How decorators are applied (inline and `@@`-augment) to models, properties, operations, and members.
- [Built-in decorators](https://typespec.io/docs/standard-library/built-in-decorators/): Constraint/format decorators (`@minLength`, `@maxLength`, `@minValue`, `@maxValue`, `@pattern`, `@format`, `@minItems`, `@maxItems`) — including applying constraints to array items via the element type.
- [Visibility](https://typespec.io/docs/language-basics/visibility/): Property lifecycle visibility (`@visibility`, `Lifecycle.Read`/`Create`/`Update`) controlling which operations expose a property.
- [ARM decorators reference](https://azure.github.io/typespec-azure/docs/libraries/azure-resource-manager/reference/decorators/): ARM-specific decorators (e.g. `@key`, `@armResourceOperations`, resource-name parameter constraints).

---

## Case 8 — Private Endpoints & Private Links (ARM)

> Add private connectivity surfaces to an ARM resource.

- [Private endpoints](https://azure.github.io/typespec-azure/docs/howtos/arm/private-endpoints/): How to model `privateEndpointConnections` child resources.
- [Private links](https://azure.github.io/typespec-azure/docs/howtos/arm/private-links/): How to model `privateLinkResources` and required operations.

---

## Case 9 — Network Security Perimeter (ARM)

> Add NSP support to an ARM resource.

- [Network security perimeter](https://azure.github.io/typespec-azure/docs/howtos/arm/network-security-perimeter/): Required child resources, operations, and properties for NSP-compliant services.

---

## Case 10 — ARM Common Types

> Reference or upgrade shared ARM common-types definitions.

- [Add common types](https://azure.github.io/typespec-azure/docs/howtos/arm/add-common-types/): How to import and pin a common-types version, and how to migrate when bumping.

---

## Case 11 — Change Provider Namespace (ARM)

> Rename or move the resource provider namespace.

- [Change provider namespace](https://azure.github.io/typespec-azure/docs/howtos/arm/change-provider-namespace/): Step-by-step procedure including versioning considerations.

---

## Case 12 — Azure Portal Defaults (ARM)

> Customize how the resource surfaces in the Azure portal.

- [Default experiences](https://azure.github.io/typespec-azure/docs/howtos/azure-portal/default-experiences/): `@azureResourceBase`, `@browse`, `@about`, and other portal decorators.

---

## Case 13 — Content Negotiation (Data-plane)

> Request/response content-type negotiation for data-plane services.

- [Content negotiation](https://azure.github.io/typespec-azure/docs/howtos/azure-core/content-negotiation/): Modeling `Accept`/`Content-Type` variants and multi-representation responses.

