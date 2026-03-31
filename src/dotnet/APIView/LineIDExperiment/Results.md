# Token Coverage Results

Input paths: Examples
Analyzed files: 24

## Methodology

Each line in a token file that carries a `LineId` is walked in tree order. A *calculated ID* is derived purely from the text content of the line and its ancestors: the text of each ancestor is sanitized (whitespace collapsed to `_`) and the chain is joined with `.` from root to the current line. If a child's sanitized text already begins with the parent's calculated ID, the parent prefix is not repeated.

A line is **reachable** if its text chain produces a non-empty calculated ID. A reachable line is **unique** when its calculated ID does not collide with any other line in the same file; otherwise it is **non-unique**. A line is **unreachable** if every token on it (after skipping keywords and punctuation) is empty or whitespace, so no discriminating text survives into the ID.

Examples of original lines that often become `empty-or-whitespace` after filtering: `@objcMembers`, `@get`, `@query`.

## Executive Summary

- Total line IDs: 69531
- Unique reachable: 37194 (53.5%)
- Non-unique reachable: 32330 (46.5%)
- Unreachable: 7 (0.0%)

## Per-Language Averages

*Avg Non-Unique%* averages each file's rate equally. *Weighted Non-Unique%* is total non-unique lines ÷ total line IDs across all files in the language, so larger files carry more weight.*

| Language | Files | Avg Line IDs | Avg Unique% | Avg Non-Unique% | Avg Unreachable% | Weighted Non-Unique% |
|---|---:|---:|---:|---:|---:|---:|
| C# | 3 | 112 | 98.8% | 1.2% | 0.0% | 2.7% |
| Go | 3 | 1495 | 100.0% | 0.0% | 0.0% | 0.0% |
| Java | 3 | 15972 | 35.2% | 64.8% | 0.0% | 62.4% |
| JavaScript | 3 | 1206 | 100.0% | 0.0% | 0.0% | 0.0% |
| Python | 3 | 1372 | 75.2% | 24.8% | 0.0% | 22.2% |
| Rust | 3 | 1232 | 88.8% | 11.0% | 0.1% | 12.7% |
| Swift | 3 | 679 | 95.4% | 4.6% | 0.0% | 1.1% |
| TypeSpec | 3 | 1109 | 71.9% | 27.9% | 0.1% | 29.9% |

## Collision Classes By Language

Collision class guide:

- `comment-or-doc-boilerplate`: repeated doc/comment markers like `/**`, `*`, `*/`.
- `path-like-identifier`: long dotted/qualified paths that repeat in many declarations.
- `short-identifier`: short repeated names such as `from`, `value`, `status`.
- `literal-numeric`: repeated numeric literals such as `0`, `0.0`, `1`.
- `namespace-or-module-declaration`: repeated namespace/module/package declaration lines.
- `annotation-or-attribute`: annotation-style lines beginning with `@`.
- `empty-or-whitespace`: no surviving text after keyword/punctuation filtering.
- `other`: collisions that do not match the buckets above.

### C#

| Collision Class | Count | Share Of Language Non-Unique |
|---|---:|---:|
| path-like-identifier | 9 | 100.0% |

Key examples:

| Group Size | Class | Existing LineId | New Calculated LineId | Original Line | File |
|---:|---|---|---|---|---|
| 3 | path-like-identifier | Azure.ResourceManager.ManagedOps.Models.ManagedOpsDesiredEnablementState.ManagedOpsDesiredEnablementState(System.String) | AzureResourceManagerManagedOpsModels.ManagedOpsDesiredEnablementState_IEquatableManagedOpsDesiredEnablementState.ManagedOpsDesiredEnablementStatevalue | public ManagedOpsDesiredEnablementState(string value);  | csharp-3.json |
| 3 | path-like-identifier | Azure.ResourceManager.ManagedOps.Models.ManagedOpsEnablementStatus.ManagedOpsEnablementStatus(System.String) | AzureResourceManagerManagedOpsModels.ManagedOpsEnablementStatus_IEquatableManagedOpsEnablementStatus.ManagedOpsEnablementStatusvalue | public ManagedOpsEnablementStatus(string value);  | csharp-3.json |
| 3 | path-like-identifier | Azure.ResourceManager.ManagedOps.Models.ManagedOpsProvisioningState.ManagedOpsProvisioningState(System.String) | AzureResourceManagerManagedOpsModels.ManagedOpsProvisioningState_IEquatableManagedOpsProvisioningState.ManagedOpsProvisioningStatevalue | public ManagedOpsProvisioningState(string value);  | csharp-3.json |

### Go

No non-unique collisions found.

### Java

| Collision Class | Count | Share Of Language Non-Unique |
|---|---:|---:|
| comment-or-doc-boilerplate | 29895 | 99.9% |
| path-like-identifier | 23 | 0.1% |
| literal-numeric | 4 | 0.0% |

Key examples:

| Group Size | Class | Existing LineId | New Calculated LineId | Original Line | File |
|---:|---|---|---|---|---|
| 304 | comment-or-doc-boilerplate | JAVADOC_LINE_12866 | com.azure.resourcemanager.netapp.models.*/ |  */  | java-3.json |
| 304 | comment-or-doc-boilerplate | JAVADOC_LINE_12862 | com.azure.resourcemanager.netapp.models./** | /**  | java-3.json |
| 125 | comment-or-doc-boilerplate | JAVADOC_LINE_29647 | com.azure.resourcemanager.netapp.models.Volume.*/ |  */  | java-3.json |
| 125 | comment-or-doc-boilerplate | JAVADOC_LINE_29640 | com.azure.resourcemanager.netapp.models.Volume./** | /**  | java-3.json |
| 121 | comment-or-doc-boilerplate | JAVADOC_LINE_29645 | com.azure.resourcemanager.netapp.models.Volume.* |  *  | java-3.json |

### JavaScript

No non-unique collisions found.

### Python

| Collision Class | Count | Share Of Language Non-Unique |
|---|---:|---:|
| path-like-identifier | 740 | 80.8% |
| comment-or-doc-boilerplate | 176 | 19.2% |

Key examples:

| Group Size | Class | Existing LineId | New Calculated LineId | Original Line | File |
|---:|---|---|---|---|---|
| 6 | path-like-identifier | azure.ai.contentunderstanding.ContentUnderstandingClient.begin_analyze_1 | azure.ai.contentunderstanding.ContentUnderstandingClientClientMixinABCPipelineClientHttpRequestHttpResponseContentUnderstandingClientConfiguration_ContextManager.begin_analyze | def begin_analyze( | python-1.json |
| 6 | comment-or-doc-boilerplate | azure.ai.contentunderstanding.ContentUnderstandingClient.begin_analyze_1.param(*) | azure.ai.contentunderstanding.ContentUnderstandingClientClientMixinABCPipelineClientHttpRequestHttpResponseContentUnderstandingClientConfiguration_ContextManager.begin_analyze.* |     *,  | python-1.json |
| 6 | comment-or-doc-boilerplate | azure.ai.contentunderstanding.ContentUnderstandingClient.begin_analyze_1.param(kwargs) | azure.ai.contentunderstanding.ContentUnderstandingClientClientMixinABCPipelineClientHttpRequestHttpResponseContentUnderstandingClientConfiguration_ContextManager.begin_analyze.**kwargsAny |     **kwargs: Any | python-1.json |
| 6 | path-like-identifier | azure.ai.contentunderstanding.ContentUnderstandingClient.begin_analyze_1.param(analyzer_id) | azure.ai.contentunderstanding.ContentUnderstandingClientClientMixinABCPipelineClientHttpRequestHttpResponseContentUnderstandingClientConfiguration_ContextManager.begin_analyze.analyzer_idstr |     analyzer_id: str,  | python-1.json |
| 6 | path-like-identifier | azure.ai.contentunderstanding.ContentUnderstandingClient.begin_analyze_1.param(processing_location) | azure.ai.contentunderstanding.ContentUnderstandingClientClientMixinABCPipelineClientHttpRequestHttpResponseContentUnderstandingClientConfiguration_ContextManager.begin_analyze.processing_locationOptionalUnionstrProcessingLocation... |     processing_location: Optional[Union[str, ProcessingLocation]] = ...,  | python-1.json |

### Rust

| Collision Class | Count | Share Of Language Non-Unique |
|---|---:|---:|
| short-identifier | 320 | 68.4% |
| other | 118 | 25.2% |
| path-like-identifier | 30 | 6.4% |

Key examples:

| Group Size | Class | Existing LineId | New Calculated LineId | Original Line | File |
|---:|---|---|---|---|---|
| 4 | other | for_AmqpSymbol.impl7.impl_From_of_AmqpSimpleValue_end_for_crate_path_value_path_AmqpSymbol | FromAmqpSimpleValuecrate::value::AmqpSymbol | impl From<AmqpSimpleValue> for crate::value::AmqpSymbol {  | rust-2.json |
| 4 | short-identifier | for_AmqpSymbol.impl7.impl_From_of_AmqpSimpleValue_end_for_crate_path_value_path_AmqpSymbol.pub_fn_from_v_AmqpSimpleValue_dash_end_Self | FromAmqpSimpleValuecrate::value::AmqpSymbol.fromvAmqpSimpleValueSelf | pub fn from(v: AmqpSimpleValue) -> Self {} | rust-2.json |
| 4 | other | for_AmqpTimestamp.impl3.impl_From_of_AmqpSimpleValue_end_for_crate_path_value_path_AmqpTimestamp | FromAmqpSimpleValuecrate::value::AmqpTimestamp | impl From<AmqpSimpleValue> for crate::value::AmqpTimestamp {  | rust-2.json |
| 4 | short-identifier | for_AmqpTimestamp.impl3.impl_From_of_AmqpSimpleValue_end_for_crate_path_value_path_AmqpTimestamp.pub_fn_from_v_AmqpSimpleValue_dash_end_Self | FromAmqpSimpleValuecrate::value::AmqpTimestamp.fromvAmqpSimpleValueSelf | pub fn from(v: AmqpSimpleValue) -> Self {} | rust-2.json |
| 4 | short-identifier | for_AmqpList.impl2.impl_From_of_AmqpValue_end_for_AmqpList | FromAmqpValueAmqpList | impl From<AmqpValue> for AmqpList {  | rust-2.json |

### Swift

| Collision Class | Count | Share Of Language Non-Unique |
|---|---:|---:|
| empty-or-whitespace | 14 | 63.6% |
| path-like-identifier | 4 | 18.2% |
| namespace-or-module-declaration | 2 | 9.1% |
| other | 2 | 9.1% |

Key examples:

| Group Size | Class | Existing LineId | New Calculated LineId | Original Line | File |
|---:|---|---|---|---|---|
| 13 | namespace-or-module-declaration | AzureCommunicationCommon | package_AzureCommunicationCommon | package AzureCommunicationCommon { | swift-2.json |
| 3 | namespace-or-module-declaration | AzureCommunicationCalling | package_AzureCommunicationCalling | package AzureCommunicationCalling { | swift-1.json |
| 2 | other | AzureCommunicationCalling.LocalVideoStreamsUpdatedEventArgs.@available(*,deprecated,message:"UseVideoStreamStateChangedEventArgsinstead") | package_AzureCommunicationCalling."Use_VideoStreamStateChangedEventArgs_instead" | @available(*, deprecated, message: "Use VideoStreamStateChangedEventArgs instead") | swift-1.json |
| 2 | path-like-identifier | AzureCommunicationCalling.Call.mute(completionHandler:@escaping(Error?)->Void).@available(*,deprecated,message:"UsemuteOutgoingAudioinstead.") | package_AzureCommunicationCalling.CallCommonCall."Use_muteOutgoingAudio_instead." | @available(*, deprecated, message: "Use muteOutgoingAudio instead.") | swift-1.json |
| 2 | path-like-identifier | AzureCommunicationCalling.Call.unmute(completionHandler:@escaping(Error?)->Void).@available(*,deprecated,message:"UseunmuteOutgoingAudioinstead.") | package_AzureCommunicationCalling.CallCommonCall."Use_unmuteOutgoingAudio_instead." | @available(*, deprecated, message: "Use unmuteOutgoingAudio instead.") | swift-1.json |

### TypeSpec

| Collision Class | Count | Share Of Language Non-Unique |
|---|---:|---:|
| path-like-identifier | 712 | 71.7% |
| empty-or-whitespace | 203 | 20.4% |
| short-identifier | 29 | 2.9% |
| literal-numeric | 26 | 2.6% |
| other | 23 | 2.3% |

Key examples:

| Group Size | Class | Existing LineId | New Calculated LineId | Original Line | File |
|---:|---|---|---|---|---|
| 24 | path-like-identifier | Microsoft.DBforPostgreSQL.SourceType.OnPremises.SourceType.OnPremises.#suppress_@azure-tools/typespec-azure-core/documentation-required_FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details | Microsoft.DBforPostgreSQL.SourceType."@azure-tools/typespec-azure-core/documentation-required"_"FIXME:_Update_justification,_follow_aka.ms/tsp/conversion-fix_for_details" | #suppress "@azure-tools/typespec-azure-core/documentation-required" "FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details" | typespec-3.json |
| 22 | path-like-identifier | Microsoft.Discovery.Workspace.MessageLogs.@removed | Microsoft.Discovery.Workspace.Versions.2026-02-01-preview | @removed(Versions.2026-02-01-preview) | typespec-2.json |
| 17 | path-like-identifier | Microsoft.Discovery.Workspace.MessageLog.name.@visibility | Microsoft.Discovery.Workspace.MessageLog.Lifecycle.Read | @visibility(Lifecycle.Read) | typespec-2.json |
| 16 | other | Microsoft.DBforPostgreSQL | Microsoft.DBforPostgreSQL | namespace Microsoft.DBforPostgreSQL  { | typespec-3.json |
| 15 | path-like-identifier | Microsoft.DBforPostgreSQL.BackupsLongTermRetentionRequest.BackupsLongTermRetentionRequest.#suppress_@azure-tools/typespec-azure-core/composition-over-inheritance_FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details | Microsoft.DBforPostgreSQL."@azure-tools/typespec-azure-core/composition-over-inheritance"_"FIXME:_Update_justification,_follow_aka.ms/tsp/conversion-fix_for_details" | #suppress "@azure-tools/typespec-azure-core/composition-over-inheritance" "FIXME: Update justification, follow aka.ms/tsp/conversion-fix for details" | typespec-3.json |
