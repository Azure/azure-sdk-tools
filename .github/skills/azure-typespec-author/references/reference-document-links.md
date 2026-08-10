# Reference Document Links

## API Version Evolution

- [Versioning overview](https://azure.github.io/typespec-azure/docs/howtos/versioning/01-about-versioning/): Overview of how API versioning works in TypeSpec Azure.
- [Add preview after preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/02-preview-after-preview/): Add a new **preview** version when the latest existing version is a **preview** version.
- [Add stable after preview](https://azure.github.io/typespec-azure/docs/howtos/versioning/03-stable-after-preview/): Add a new **stable** version when the latest existing version is a **preview** version — i.e. promote/collapse the preview into the new stable.
- [Add preview after stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/04-preview-after-stable/): Add a new **preview** version when the latest existing version is a **stable** version.
- [Add stable after stable](https://azure.github.io/typespec-azure/docs/howtos/versioning/05-stable-after-stable/): Add a new **stable** version when the latest existing version is a **stable** version.
- [Evolving APIs](https://azure.github.io/typespec-azure/docs/howtos/versioning/06-evolving-apis/): How to evolve your API across versions by adding, removing, or modifying resources, operations, and properties using versioning decorators.

## ARM (Resource-Manager) Operations

- [ARM resource operations](https://azure.github.io/typespec-azure/docs/howtos/arm/resource-operations/): Standard ARM operation templates — GET (`ArmResourceRead`), PUT (`ArmResourceCreateOrReplaceSync/Async`), PATCH (`ArmCustomPatchSync/Async`), DELETE (`ArmResourceDeleteSync/DeleteWithoutOkAsync`), check-existence (`ArmResourceCheckExistence`), list (`ArmResourceListByParent`, `ArmListBySubscription`), custom actions (`ArmResourceActionSync/Async`, `ArmResourceActionNoContentSync`), and provider actions. 
- [Extension resource sample](https://azure.github.io/typespec-azure/docs/samples/resource-manager/resource-types/specific-extension/): How to define an extension resource (`model X is ExtensionResource<XProperties>`) and its operations via the `Extension.*` templates (`Extension.Read`, `Extension.CreateOrReplaceAsync`, `Extension.CustomPatchSync`, `Extension.DeleteWithoutOkAsync`, `Extension.ListByTarget`, `Extension.ActionSync`).

## Data-Plane Operations

- [Azure.Core reference](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference): Full reference for Azure.Core decorators, interfaces, operations, and models.
- [Standard resource operations](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/interfaces): Azure.Core operation templates (ResourceRead, ResourceList, ResourceCreateOrUpdate, ResourceDelete, etc.).
- [Data-plane getting started](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step01): Getting started guide for creating data-plane TypeSpec services with Azure.Core.
- [Deep Dive: Long-running (Asynchronous) Operations](https://azure.github.io/typespec-azure/docs/howtos/azure-core/long-running-operations/): Defining asynchronous (long-running) operations
