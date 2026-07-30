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
- [Standard resource operations](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/interfaces): Azure.Core operation templates (`ResourceRead`, `ResourceList`, `ResourceCreateOrUpdate`, `ResourceDelete`, etc.).
- [Data-plane getting started](https://azure.github.io/typespec-azure/docs/getstarted/azure-core/step01): Getting started guide for creating data-plane TypeSpec services with Azure.Core.
- [Deep Dive: Long-running (Asynchronous) Operations](https://azure.github.io/typespec-azure/docs/howtos/azure-core/long-running-operations/): Defining asynchronous (long-running) operations.

## Authentication and Security Definitions

- [Azure TypeSpec style guide — Security Definitions](https://azure.github.io/typespec-azure/docs/reference/azure-style-guide/#security-definitions): Azure-specific requirements. Data-plane services must explicitly apply `@useAuth`; use OAuth2 or a header API key, document every scheme, and define at least one OAuth2 scope. ARM services receive the standard security definitions from Azure.ResourceManager.
- [TypeSpec HTTP authentication](https://typespec.io/docs/libraries/http/authentication/): Syntax and semantics for `@useAuth`, `BearerAuth`, `ApiKeyAuth`, `OAuth2Auth`, scopes, and auth inheritance. A tuple means all schemes are required; a union means any one scheme is accepted. Child `@useAuth` declarations override the parent unless all desired schemes are restated.
- [Azure.Core authentication data types](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/data-types/#aadoauth2auth): Prefer Azure.Core's `AadOauth2Auth` for Microsoft Entra ID OAuth2 and `AzureApiKeyAuthentication` for the standard `Ocp-Apim-Subscription-Key` header when those standard Azure schemes match the service.

## Error Responses

- [Azure.Core `no-error-status-codes` rule](https://azure.github.io/typespec-azure/docs/libraries/azure-core/rules/no-error-status-codes/): Azure data-plane APIs should normally use one `default` error response instead of defining separate custom 4xx or 5xx responses.
- [Azure.Core operation templates](https://azure.github.io/typespec-azure/docs/libraries/azure-core/reference/interfaces/): Azure.Core resource-operation templates include a default error response and allow an `ErrorResponse` template argument only when a service-specific error contract is required.
- [TypeSpec handling errors](https://typespec.io/docs/getting-started/getting-started-rest/03-handling-errors/): General TypeSpec guidance for structured custom error models. Mark custom error models with `@error` and include them in an operation response union. For Azure services, apply the stricter Azure.Core default-error guidance above before modeling individual status-code responses.
