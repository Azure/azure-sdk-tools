# Reference Document Links

## Add Resource Type (ARM)

## Add Resource Operations (ARM)

## API Versioning

- [Versioning overview](https://azure.github.io/typespec-azure/docs/howtos/versioning/01-about-versioning/): Overview of how API versioning works in TypeSpec Azure.
- [preview → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/02-preview-after-preview/): How to add a new preview version after an existing preview version.
- [preview → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/): How to promote a preview version to stable.
- [stable → preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/04-preview-after-stable/): How to add a new preview version after a stable version.
- [stable → stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/05-stable-after-stable/): How to add a new stable version after an existing stable version.
- [Evolving APIs](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/): How to evolve ARM and data-plane APIs by adding, removing, renaming, or modifying resources, operations, models, and properties with versioning decorators.

## Long-Running Operations (LRO)

- [Deep Dive: Long-running (Asynchronous) Operations](https://azure.github.io/typespec-azure/docs/howtos/azure-core/long-running-operations/): Azure.Core operation templates and metadata for standard and custom asynchronous operation patterns.

## Paging

- [TypeSpec pagination](https://typespec.io/docs/standard-library/pagination/): Core paging patterns for `@list`, `@pageItems`, `@nextLink`, `@continuationToken`, `@pageSize`, and `@offset`.
- [Paging operations for generated clients](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/05pagingoperations/): SDK paging behavior for `value`/`nextLink` responses and continuation-token pagination.
- [Azure.Core reference](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/): Azure.Core `ResourceList`, page models, and standard list query parameters including top and skip.
- [Azure.Core interfaces and operations](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/interfaces/): Standard Azure.Core resource operation templates, including `ResourceList`.

## Models and Enums

## Decorators

- [TypeSpec decorators](https://typespec.io/docs/language-basics/decorators/): Decorator syntax, targets, arguments, and augment decorators using `@@`.
- [HTTP authentication](https://typespec.io/docs/libraries/http/authentication/): Configure bearer, API-key, OAuth2, and combined authentication schemes with `@useAuth`.
- [Client customization](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/03client/): Customize root clients, sub-clients, operation groups, client names, and client namespaces.
- [Renaming generated client types and methods](https://azure.github.io/typespec-azure/docs/howtos/generate-client-libraries/09renaming/): Use `@clientName` in `client.tsp` to rename generated models, properties, operations, and parameters globally or per language.
- [Client Generator Core decorators](https://azure.github.io/typespec-azure/docs/libraries/typespec-client-generator-core/reference/decorators/): Reference for SDK-shaping decorators including `@access`, `@client`, `@clientName`, `@clientNamespace`, and `@operationGroup`.

## Warnings
