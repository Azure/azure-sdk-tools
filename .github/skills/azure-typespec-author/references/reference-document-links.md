# Reference Document Links

Curated catalog of authoritative TypeSpec Azure documentation, **categorized by authoring case**. The cases are derived from the structure of the [TypeSpec Azure documentation](https://azure.github.io/typespec-azure/docs/intro/) (`docs/howtos/`, `docs/getstarted/`) and, for core language concepts (models, enums, paging, decorators, suppressions), the underlying [TypeSpec core documentation](https://typespec.io/docs) that the Azure libraries build on.

For each case, Step 3 (`authoring-plan.md`) selects the listed URLs as input to [agentic search](agentic-search.md). Select only the URLs relevant to the request; if no case matches exactly, use the **General References** section plus any URLs whose title matches request keywords. If **nothing** here is relevant to the request, treat the request as not covered by this catalog and use the **KB fallback** (`authoring-plan.md` Step 3.3) instead.

The catalog is organized into **8 cases**:

1. Add Resource Type (ARM)
2. Add Resource Operations (ARM)
3. API Versioning
4. Long-Running Operations (LRO)
5. Paging
6. Models and Enums
7. Decorators
8. Warnings

Sources are limited to <https://azure.github.io/typespec-azure/> and <https://typespec.io/docs/>.

---

## General References (always available)

- [Introduction](https://azure.github.io/typespec-azure/docs/intro/): Overview of TypeSpec Azure libraries, emitters, and intended artifacts (OpenAPI 2/3).
- [Installation](https://azure.github.io/typespec-azure/docs/getstarted/installation/): Install TypeSpec compiler and Azure libraries.
- [Create a project](https://azure.github.io/typespec-azure/docs/getstarted/createproject/): Bootstrap a new TypeSpec project.
- [TypeSpec language basics](https://typespec.io/docs/language-basics/overview/): Core language primitives (namespaces, models, operations, decorators) used by every `.tsp` file.

---

## Case 1 — Add Resource Type (ARM)

> Define a new ARM resource (tracked, proxy, extension, or child) with its standard operations. Also covers specialized ARM resource surfaces (private endpoints/links, network security perimeter), shared common-types, and provider-namespace changes.

- [Resource type](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/): How to declare ARM resource types (`TrackedResource`, `ProxyResource`, `ExtensionResource`, singleton variants), parent/child hierarchies, and naming.
- [Resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): Standard CRUD interfaces (`ArmResourceCreateOrReplaceAsync`, `ArmResourceRead`, `ArmCustomPatch`, `ArmResourceDeleteAsync`, list-by-parent / list-by-subscription) and the `Extension.*` operation templates for extension resources.
- [ARM rules](https://azure.github.io/typespec-azure/docs/howtos/arm/arm-rules/): Mandatory linter/RPC rules that new resources must satisfy.
- [RPC guidelines coverage](https://azure.github.io/typespec-azure/docs/howtos/arm/rpc-guidelines-coverage/): Mapping of TypeSpec ARM patterns to ARM RPC guideline requirements.
- [Getting started — ARM step 02 (Defining the Resources)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step02/) and [step 03 (Defining Child Resource Types)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step03/): End-to-end walkthroughs of adding tracked/proxy and child resources.
- [Private endpoints](https://azure.github.io/typespec-azure/docs/howtos/arm/private-endpoints/): Model `privateEndpointConnections` child resources on an ARM resource.
- [Private links](https://azure.github.io/typespec-azure/docs/howtos/arm/private-links/): Model `privateLinkResources` and their required operations.
- [Network security perimeter](https://azure.github.io/typespec-azure/docs/howtos/arm/network-security-perimeter/): Required child resources, operations, and properties for NSP-compliant services.
- [Add common types](https://azure.github.io/typespec-azure/docs/howtos/arm/add-common-types/): Import and pin an ARM common-types version, and migrate when bumping (used by the resource envelope and shared models).
- [Change provider namespace](https://azure.github.io/typespec-azure/docs/howtos/arm/change-provider-namespace/): Rename or move the resource-provider namespace, including versioning considerations.

---

## Case 2 — Add Resource Operations (ARM)

> Add CRUD, list, or custom actions to an existing ARM resource, including content-type negotiation for the request/response.

- [Resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): All built-in ARM operation templates and how to compose them.
- [Long-running operations (ARM)](https://azure.github.io/typespec-azure/docs/howtos/arm/long-running-operations/): Async PUT/DELETE/POST patterns, `ArmCombinedLroHeaders`, `LroHeaders = ArmCombinedLroHeaders<FinalResult = ...>`.
- [ARM rules](https://azure.github.io/typespec-azure/docs/howtos/arm/arm-rules/): Linter rules covering operation shape, response codes, and pagination.
- [RPC guidelines coverage](https://azure.github.io/typespec-azure/docs/howtos/arm/rpc-guidelines-coverage/): Required vs optional operations per resource category.
- [Getting started — ARM step 04 (Defining Custom Actions)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step04/): Walkthrough of adding custom POST actions and action templates (`ArmResourceActionSync`, `ArmResourceActionAsync`) to a resource.
- [Getting started — ARM step 05 (Defining long-running resource operations)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step05/): Walkthrough of defining async ARM operations.
- [Content negotiation](https://azure.github.io/typespec-azure/docs/howtos/azure-core/content-negotiation/): Model `Accept`/`Content-Type` variants and multi-representation responses (data-plane).

---

## Case 3 — API Versioning

> Add, bump, or promote an API version (preview ↔ stable), **or** add/update code (resources, operations, models, properties, enum members, defaults, optionality) scoped to a specific existing version using versioning decorators.

- [Versioning overview](https://azure.github.io/typespec-azure/docs/howtos/versioning/01-about-versioning/): Concepts, decorators (`@added`, `@removed`, `@renamedFrom`, `@madeOptional`, `@madeRequired`, `@typeChangedFrom`), and the `Versions` enum.
- [preview → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/02-preview-after-preview/): Add a new preview version after an existing preview.
- [preview → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/): Promote a preview to stable.
- [stable → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/04-preview-after-stable/): Add a preview after a stable version.
- [stable → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/05-stable-after-stable/): Add a new stable after an existing stable version.
- [Evolving APIs](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/): Add, remove, or modify resources, operations, and properties across versions — including `@@`-augment decorators (`@@added(Model.member, Versions.x)`) for spread/augmented members.
- [Versioning (getstarted)](https://azure.github.io/typespec-azure/docs/getstarted/versioning/): Introduction to versioning in TypeSpec Azure projects.
- [Uncommon: Perpetual preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/uncommon-scenarios/02-perpetual-preview/): Long-lived preview-only services.
- [Uncommon: Converting specs](https://azure.github.io/typespec-azure/docs/howtos/versioning/uncommon-scenarios/01-converting-specs/): Converting a spec with multiple preview versions into a single latest preview version, handling versioning decorator ordering.

---

## Case 4 — Long-Running Operations (LRO)

> Author async operations for ARM or data-plane services.

- [Long-running operations (ARM)](https://azure.github.io/typespec-azure/docs/howtos/arm/long-running-operations/): ARM-specific LRO templates and headers (`ArmCombinedLroHeaders`, `ArmLroLocationHeader`, final-state-via).
- [Long-running operations (Azure Core)](https://azure.github.io/typespec-azure/docs/howtos/azure-core/long-running-operations/): Data-plane LRO patterns using `@azure-tools/typespec-azure-core` (`LongRunningRpcOperation`, `Azure.Core.Foundations` poll/status models).
- [Getting started — Azure Core step 05 (Defining long-running resource operations)](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step05/): Tutorial walkthrough of an LRO for a data-plane resource.
- [Getting started — ARM step 05 (Defining long-running resource operations)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step05/): Tutorial walkthrough of an async ARM operation.

---

## Case 5 — Paging

> Model paged (list) operations and custom pagination for ARM or data-plane services, including standard list query parameters (`$top`, `$skip`, `$filter`).

- [Pagination (TypeSpec core)](https://typespec.io/docs/standard-library/pagination/): Core paging decorators (`@list`, `@pageItems`, `@nextLink`, `@continuationToken`, `@offset`, `@pageSize`) used to describe paged responses.
- [Resource operations (ARM)](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): ARM list templates (`ArmResourceListByParent`, `ArmListBySubscription`, `ArmResourceListAtScope`) that emit standard `value` + `nextLink` paged envelopes, and standard list query parameters (`Azure.Core.TopQueryParameter`/`SkipQueryParameter`, `StandardListQueryParameters`) — prefer these over hand-written `@query("$top")`/`@query("$skip")`.
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
- [Scalars (TypeSpec core)](https://typespec.io/docs/language-basics/scalars/): Built-in scalar types (`string`, `int32`, `float64`, `boolean`, `utcDateTime`, `url`, etc.) and how to declare custom scalars.
- [Getting started — Azure Core step 03 (Defining your first resource)](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step03/): Modeling a resource and its properties in a data-plane service.
- [Getting started — ARM step 02 (Defining the Resources)](https://azure.github.io/typespec-azure/docs/getstarted/azure-resource-manager/step02/): Modeling ARM resource properties and the `properties` envelope.

---

## Case 7 — Decorators

> Apply decorators to models, properties, operations, and members — inline or via `@@`-augment — including constraint/format, visibility, ARM-specific, and Azure-portal decorators.

- [Decorators (TypeSpec core)](https://typespec.io/docs/language-basics/decorators/): How decorators are applied (inline and `@@`-augment) to models, properties, operations, and members.
- [Built-in decorators](https://typespec.io/docs/standard-library/built-in-decorators/): Constraint/format decorators (`@minLength`, `@maxLength`, `@minValue`, `@maxValue`, `@pattern`, `@format`, `@minItems`, `@maxItems`) — including applying constraints to array items via the element type.
- [Visibility](https://typespec.io/docs/language-basics/visibility/): Property lifecycle visibility (`@visibility`, `Lifecycle.Read`/`Create`/`Update`) controlling which operations expose a property.
- [ARM decorators reference](https://azure.github.io/typespec-azure/docs/libraries/azure-resource-manager/reference/decorators/): ARM-specific decorators (e.g. `@key`, `@armResourceOperations`, resource-name parameter constraints).
- [Azure Core decorators reference](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/decorators/): Azure Core decorators (`@finalLocation`, `@pollingOperation`, `@lroStatus`, `@pagedResult`, `@items`, `@nextLink`) for LRO and paging patterns.
- [Azure portal default experiences](https://azure.github.io/typespec-azure/docs/howtos/azure-portal/default-experiences/): Portal decorators (`@azureResourceBase`, `@browse`, `@about`, …) that customize how a resource surfaces in the Azure portal.

---

## Case 8 — Warnings

> Suppress compiler/linter warnings and diagnostics produced by `tsp compile .`, with a justification.

- [Suppress warnings (TypeSpec Azure)](https://azure.github.io/typespec-azure/docs/troubleshoot/suppresswarnings/): Suppress a warning/diagnostic with an inline `#suppress "<ErrCode>" "<ReasonMsg>"` statement placed directly above the offending TypeSpec statement; the error code is the string reported in the compiler output.
- [Directives — `#suppress` (TypeSpec core)](https://typespec.io/docs/language-basics/directives/): The `#suppress "<diagnostic-code>" "<justification>"` directive attaches to a syntax node to suppress a specific warning diagnostic. **Inline `#suppress` is the only mechanism for non-linter / library diagnostics** (e.g. `@azure-tools/typespec-client-generator-core/property-name-conflict`), which `tspconfig.yaml` cannot disable.
- [Configuration — `linter.disable` (TypeSpec core)](https://typespec.io/docs/handbook/configuration/configuration/): Disable a **linter rule** project-wide in `tspconfig.yaml` under `linter.disable` (with a reason). Note: `linter.disable` affects **only linter rules** — prefer inline `#suppress` when the task asks to suppress specific warnings in the spec, and use it for any non-linter diagnostic.
