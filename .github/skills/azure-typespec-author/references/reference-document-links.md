# Reference Document Links

## API Version Evolution

- [Versioning overview](https://azure.github.io/typespec-azure/docs/howtos/versioning/01-about-versioning/): Overview of how API versioning works in TypeSpec Azure.
- [preview → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/02-preview-after-preview/): How to add a new preview version after an existing preview version.
- [preview → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/): How to promote a preview version to stable.
- [stable → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/04-preview-after-stable/): How to add a new preview version after a stable version.
- [stable → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/05-stable-after-stable/): How to add a new stable version after an existing stable version.
- [Evolving APIs](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/): How to evolve your API across versions by adding, removing, renaming, or changing resources, operations, models, and properties with versioning decorators.

## ARM Resource Types and Operations

- [ARM resource types](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-type/): Choose and model tracked, proxy, tenant, extension, child, subscription, location, and singleton resources. Includes resource-name parameter patterns and complete resource examples.
- [ARM resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): Required and recommended CRUDL operations, synchronous and asynchronous templates, custom PATCH models, list scopes, HEAD checks, and POST resource actions.
- [ARM long-running operations](https://azure.github.io/typespec-azure/docs/howtos/arm/long-running-operations/): Select and customize LRO polling headers for PUT, PATCH, DELETE, and POST operations; ensure `FinalResult` matches the operation result.
- [ARM rules, linting, and suppression](https://azure.github.io/typespec-azure/docs/howtos/arm/arm-rules/): Interpret ARM diagnostics, fix the underlying rule violation when possible, and use a targeted `#suppress` with a concrete justification only for approved exceptions or false positives.

## Data-Plane Operations

- [Azure.Core reference](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference): Full reference for Azure.Core decorators, interfaces, operations, and models.
- [Standard resource operations](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/interfaces): Azure.Core operation templates (`ResourceRead`, `ResourceList`, `ResourceCreateOrUpdate`, `ResourceDelete`, and related templates).
- [Data-plane getting started](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step01): Getting started guide for creating data-plane TypeSpec services with Azure.Core.
- [Deep Dive: Long-running (Asynchronous) Operations](https://azure.github.io/typespec-azure/docs/howtos/azure-core/long-running-operations/): Define and customize asynchronous data-plane operations.
