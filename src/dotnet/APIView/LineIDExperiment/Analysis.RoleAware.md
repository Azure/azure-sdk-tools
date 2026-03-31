# Role-Aware Token Coverage Results

Input paths: .\Examples
Analyzed files: 24

## Methodology

This report uses a role-aware parser that classifies each `LineId` line as `doc`, `attribute`, `signature`, or `other`, then computes role-specific fingerprints. Signatures include overload-sensitive shape (`name`, arity, generic arity, and type profile). Docs and attributes are stabilized with deterministic owner-scoped ordinals. A final collision backoff ordinal is only applied when a collision still remains.

## Executive Summary

- Total line IDs: 69531
- Base unique reachable: 38036 (54.7%)
- Base non-unique reachable: 31495 (45.3%)
- Unreachable: 0 (0.0%)
- Final unique (after role + backoff): 69531 (100.0%)
- Final non-unique (after role + backoff): 0 (0.0%)

## Issue Class Coverage

| Class | Base Collision Count | Share Of Base Non-Unique |
|---|---:|---:|
| overload-signature | 2165 | 6.9% |
| doc-comment | 24772 | 78.7% |
| decorator-annotation | 41 | 0.1% |

## Per-Language

| Language | Files | Avg Line IDs | Base Non-Unique% | Final Non-Unique% |
|---|---:|---:|---:|---:|
| C# | 3 | 112 | 10.7% | 0.0% |
| Go | 3 | 1495 | 9.5% | 0.0% |
| Java | 3 | 15972 | 52.3% | 0.0% |
| JavaScript | 3 | 1206 | 81.6% | 0.0% |
| Python | 3 | 1372 | 49.0% | 0.0% |
| Rust | 3 | 1232 | 19.8% | 0.0% |
| Swift | 3 | 679 | 9.3% | 0.0% |
| TypeSpec | 3 | 1109 | 2.6% | 0.0% |

## Key Collision Examples (Base)

### overload-signature

| Language | Existing LineId | Base Fingerprint | Original Line |
|---|---|---|---|
| C# | Azure.Messaging.EventHubs.EventProcessorClient.EventProcessorClient(Azure.Storage.Blobs.BlobContainerClient, System.String, System.String, System.String, Azure.AzureNamedKeyCredential, Azure.Messaging.EventHubs.EventProcessorClientOptions) | SIG\|public_class_EventProcessorClient_:_EventProcessor<EventProcessorPartition>_{\|EventProcessorClient\|a6\|g0\|tk2-3-1-3-0-1-2-0-1-2-0-1-2-0-1-3-0-1-3-0 | public EventProcessorClient(BlobContainerClient checkpointStore, string consumerGroup, string fullyQualifiedNamespace, string eventHubName, AzureNamedKeyCredent |
| C# | Azure.Messaging.EventHubs.EventProcessorClient.EventProcessorClient(Azure.Storage.Blobs.BlobContainerClient, System.String, System.String, System.String, Azure.AzureSasCredential, Azure.Messaging.EventHubs.EventProcessorClientOptions) | SIG\|public_class_EventProcessorClient_:_EventProcessor<EventProcessorPartition>_{\|EventProcessorClient\|a6\|g0\|tk2-3-1-3-0-1-2-0-1-2-0-1-2-0-1-3-0-1-3-0 | public EventProcessorClient(BlobContainerClient checkpointStore, string consumerGroup, string fullyQualifiedNamespace, string eventHubName, AzureSasCredential c |
| C# | Azure.Messaging.EventHubs.EventProcessorClient.EventProcessorClient(Azure.Storage.Blobs.BlobContainerClient, System.String, System.String, System.String, Azure.Core.TokenCredential, Azure.Messaging.EventHubs.EventProcessorClientOptions) | SIG\|public_class_EventProcessorClient_:_EventProcessor<EventProcessorPartition>_{\|EventProcessorClient\|a6\|g0\|tk2-3-1-3-0-1-2-0-1-2-0-1-2-0-1-3-0-1-3-0 | public EventProcessorClient(BlobContainerClient checkpointStore, string consumerGroup, string fullyQualifiedNamespace, string eventHubName, TokenCredential cred |
| C# | Azure.ResourceManager.ManagedOps.ManagedOpData | SIG\|namespace_Azure.ResourceManager.ManagedOps_{\|ManagedOpData\|a0\|g1\|tk2-2-3-1-3-1-3-1-3-1-1-3-1-3-1-1 | public class ManagedOpData : ResourceData, IJsonModel<ManagedOpData>, IPersistableModel<ManagedOpData> { |
| C# | Azure.ResourceManager.ManagedOps.ManagedOpResource | SIG\|namespace_Azure.ResourceManager.ManagedOps_{\|ManagedOpData\|a0\|g1\|tk2-2-3-1-3-1-3-1-3-1-1-3-1-3-1-1 | public class ManagedOpResource : ArmResource, IJsonModel<ManagedOpData>, IPersistableModel<ManagedOpData> { |
| C# | Azure.ResourceManager.ManagedOps.Mocking.MockableManagedOpsArmClient | SIG\|namespace_Azure.ResourceManager.ManagedOps.Mocking_{\|ArmResource\|a0\|g0\|tk2-2-3-1-3-1 | public class MockableManagedOpsArmClient : ArmResource { |
| Go | armnetapp-(client *AccountsClient) BeginChangeKeyVault | SIG\|armnetapp.AccountsClient\|func\|a1\|g0\|tk2-0-1-3-1-3-1-4-3-1-4-3-1-4-3-1-4-1-3-1 | func (*AccountsClient) BeginChangeKeyVault(ctx context.Context, resourceGroupName string, accountName string, options *AccountsClientBeginChangeKeyVaultOptions) |
| Go | armnetapp-(client *AccountsClient) BeginCreateOrUpdate | SIG\|armnetapp.AccountsClient\|func\|a1\|g0\|tk2-0-1-3-1-3-1-4-3-1-4-3-1-4-3-1-4-3-1-4 | func (*AccountsClient) BeginCreateOrUpdate(ctx context.Context, resourceGroupName string, accountName string, body Account, options *AccountsClientBeginCreateOr |
| Go | armnetapp-(client *AccountsClient) BeginDelete | SIG\|armnetapp.AccountsClient\|func\|a1\|g0\|tk2-0-1-3-1-3-1-4-3-1-4-3-1-4-3-1-4-1-3-1 | func (*AccountsClient) BeginDelete(ctx context.Context, resourceGroupName string, accountName string, options *AccountsClientBeginDeleteOptions) (*runtime.Polle |
| Go | armdiscovery-(client *BookshelfPrivateEndpointConnectionsClient) BeginCreateOrUpdate | SIG\|armdiscovery.BookshelfPrivateEndpointConnectionsClient\|func\|a1\|g0\|tk2-0-1-3-1-3-1-4-3-1-4-3-1-4-3-1-4-3-1-4 | func (*BookshelfPrivateEndpointConnectionsClient) BeginCreateOrUpdate(ctx context.Context, resourceGroupName string, bookshelfName string, privateEndpointConnec |

### doc-comment

| Language | Existing LineId | Base Fingerprint | Original Line |
|---|---|---|---|
| Java | JAVADOC_LINE_1 | DOC\|package-com.azure.resourcemanager.dynatrace\|doc_body | * Package containing the classes for DynatraceObservability. |
| Java | JAVADOC_LINE_2 | DOC\|package-com.azure.resourcemanager.dynatrace\|doc_body | * null. |
| Java | JAVADOC_LINE_7 | DOC\|com.azure.resourcemanager.dynatrace.DynatraceManager.public-static-DynatraceManager-authenticate(TokenCredential-credential,-AzureProfile-profile)\|doc_marker | /** |
| Java | JAVADOC_LINE_1 | DOC\|package-com.azure.ai.contentsafety\|doc_body | * Package containing the classes for ContentSafety. |
| Java | JAVADOC_LINE_2 | DOC\|package-com.azure.ai.contentsafety\|doc_body | * Analyze harmful content. |
| Java | JAVADOC_LINE_7 | DOC\|com.azure.ai.contentsafety.BlocklistAsyncClient.public-Mono<AddOrUpdateTextBlocklistItemsResult>-addOrUpdateBlocklistItems(String-name,-AddOrUpdateTextBlocklistItemsOptions-opt | /** |
| Java | JAVADOC_LINE_1 | DOC\|package-com.azure.resourcemanager.netapp\|doc_body | * Package containing the classes for NetAppFiles. |
| Java | JAVADOC_LINE_2 | DOC\|package-com.azure.resourcemanager.netapp\|doc_body | * Microsoft NetApp Files Azure Resource Provider specification. |
| Java | JAVADOC_LINE_5 | DOC\|com.azure.resourcemanager.netapp.NetAppFilesManager\|doc_body | * Entry point to NetAppFilesManager. |

### decorator-annotation

| Language | Existing LineId | Base Fingerprint | Original Line |
|---|---|---|---|
| C# | System.ClientModel.Primitives.ModelReaderWriterBuildableAttribute(Azure.ResourceManager.ManagedOps.Models.AzureMonitorConfiguration).Azure.ResourceManager.ManagedOps.AzureResourceManagerManagedOpsContext | ATTR\|Azure.ResourceManager.ManagedOps.AzureResourceManagerManagedOpsContext\|ModelReaderWriterBuildable | [ModelReaderWriterBuildable(typeof(AzureMonitorConfiguration))] |
| C# | System.ClientModel.Primitives.ModelReaderWriterBuildableAttribute(Azure.ResourceManager.ManagedOps.Models.ChangeTrackingConfiguration).Azure.ResourceManager.ManagedOps.AzureResourceManagerManagedOpsContext | ATTR\|Azure.ResourceManager.ManagedOps.AzureResourceManagerManagedOpsContext\|ModelReaderWriterBuildable | [ModelReaderWriterBuildable(typeof(ChangeTrackingConfiguration))] |
| C# | System.ClientModel.Primitives.ModelReaderWriterBuildableAttribute(Azure.ResourceManager.ManagedOps.Models.DefenderCspmInformation).Azure.ResourceManager.ManagedOps.AzureResourceManagerManagedOpsContext | ATTR\|Azure.ResourceManager.ManagedOps.AzureResourceManagerManagedOpsContext\|ModelReaderWriterBuildable | [ModelReaderWriterBuildable(typeof(DefenderCspmInformation))] |
| TypeSpec | Search.facets.facets.#suppress_@azure-tools/typespec-azure-core/use-standard-names_ | ATTR\|Search.facets\|suppress | #suppress "@azure-tools/typespec-azure-core/use-standard-names" "" |
| TypeSpec | Search.facets.facets.#suppress_@azure-tools/typespec-azure-core/use-standard-operations_Routes for listing and single are almost the same, requires custom operation | ATTR\|Search.facets\|suppress | #suppress "@azure-tools/typespec-azure-core/use-standard-operations" "Routes for listing and single are almost the same, requires custom operation" |
| TypeSpec | Search.search.search.#suppress_@azure-tools/typespec-azure-core/use-standard-names_ | ATTR\|Search.search\|suppress | #suppress "@azure-tools/typespec-azure-core/use-standard-names" "" |
| TypeSpec | Microsoft.Discovery.Workspace.Investigations.updateDiscoveryEngine.Investigations.updateDiscoveryEngine.#suppress_@typespec/http/patch-implicit-optional_ | ATTR\|Microsoft.Discovery.Workspace.Investigations.updateDiscoveryEngine\|suppress | #suppress "@typespec/http/patch-implicit-optional" "" |
| TypeSpec | Microsoft.Discovery.Workspace.Investigations.updateDiscoveryEngine.Investigations.updateDiscoveryEngine.#suppress_@azure-tools/typespec-azure-core/use-standard-operations | ATTR\|Microsoft.Discovery.Workspace.Investigations.updateDiscoveryEngine\|suppress | #suppress "@azure-tools/typespec-azure-core/use-standard-operations" |
| TypeSpec | Microsoft.DBforPostgreSQL.CustomErrorResponse.CustomErrorResponse.#suppress_@azure-tools/typespec-azure-core/composition-over-inheritance_FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details | ATTR\|Microsoft.DBforPostgreSQL.CustomErrorResponse\|suppress | #suppress "@azure-tools/typespec-azure-core/composition-over-inheritance" "FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details" |
| TypeSpec | Microsoft.DBforPostgreSQL.CustomErrorResponse.CustomErrorResponse.#suppress_@azure-tools/typespec-azure-core/documentation-required_FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details | ATTR\|Microsoft.DBforPostgreSQL.CustomErrorResponse\|suppress | #suppress "@azure-tools/typespec-azure-core/documentation-required" "FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details" |
