import { PollerLike, OperationState } from "@azure/core-lro";
import { DataType, DataTypeUpdate, ContainerSaS, ContainerSasToken, DataTypeListResult } from "../../models/models.js";
import { PagedAsyncIterableIterator } from "../../models/pagingTypes.js";
import { DataTypesCreate200Response, DataTypesCreate201Response, DataTypesCreateDefaultResponse, DataTypesCreateLogicalResponse, DataTypesDelete202Response, DataTypesDelete204Response, DataTypesDeleteData202Response, DataTypesDeleteData204Response, DataTypesDeleteDataDefaultResponse, DataTypesDeleteDataLogicalResponse, DataTypesDeleteDefaultResponse, DataTypesDeleteLogicalResponse, DataTypesGenerateStorageContainerSasToken200Response, DataTypesGenerateStorageContainerSasTokenDefaultResponse, DataTypesGet200Response, DataTypesGetDefaultResponse, DataTypesListByDataProduct200Response, DataTypesListByDataProductDefaultResponse, DataTypesUpdate200Response, DataTypesUpdate202Response, DataTypesUpdateDefaultResponse, DataTypesUpdateLogicalResponse, NetworkAnalyticsContext as Client } from "../../rest/index.js";
import { StreamableMethod } from "@azure-rest/core-client";
import { DataTypesCreateOptionalParams, DataTypesGetOptionalParams, DataTypesUpdateOptionalParams, DataTypesDeleteOptionalParams, DataTypesDeleteDataOptionalParams, DataTypesGenerateStorageContainerSasTokenOptionalParams, DataTypesListByDataProductOptionalParams } from "../../models/options.js";
export declare function _createSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, resource: DataType, options?: DataTypesCreateOptionalParams): StreamableMethod<DataTypesCreate200Response | DataTypesCreate201Response | DataTypesCreateDefaultResponse | DataTypesCreateLogicalResponse>;
export declare function _createDeserialize(result: DataTypesCreate200Response | DataTypesCreate201Response | DataTypesCreateDefaultResponse | DataTypesCreateLogicalResponse): Promise<DataType>;
/** Create data type resource. */
export declare function create(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, resource: DataType, options?: DataTypesCreateOptionalParams): PollerLike<OperationState<DataType>, DataType>;
export declare function _getSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, options?: DataTypesGetOptionalParams): StreamableMethod<DataTypesGet200Response | DataTypesGetDefaultResponse>;
export declare function _getDeserialize(result: DataTypesGet200Response | DataTypesGetDefaultResponse): Promise<DataType>;
/** Retrieve data type resource. */
export declare function get(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, options?: DataTypesGetOptionalParams): Promise<DataType>;
export declare function _updateSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, properties: DataTypeUpdate, options?: DataTypesUpdateOptionalParams): StreamableMethod<DataTypesUpdate200Response | DataTypesUpdate202Response | DataTypesUpdateDefaultResponse | DataTypesUpdateLogicalResponse>;
export declare function _updateDeserialize(result: DataTypesUpdate200Response | DataTypesUpdate202Response | DataTypesUpdateDefaultResponse | DataTypesUpdateLogicalResponse): Promise<DataType>;
/** Update data type resource. */
export declare function update(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, properties: DataTypeUpdate, options?: DataTypesUpdateOptionalParams): PollerLike<OperationState<DataType>, DataType>;
export declare function _$deleteSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, options?: DataTypesDeleteOptionalParams): StreamableMethod<DataTypesDelete202Response | DataTypesDelete204Response | DataTypesDeleteDefaultResponse | DataTypesDeleteLogicalResponse>;
export declare function _$deleteDeserialize(result: DataTypesDelete202Response | DataTypesDelete204Response | DataTypesDeleteDefaultResponse | DataTypesDeleteLogicalResponse): Promise<void>;
/** Delete data type resource. */
/**
 *  @fixme delete is a reserved word that cannot be used as an operation name.
 *         Please add @clientName("clientName") or @clientName("<JS-Specific-Name>", "javascript")
 *         to the operation to override the generated name.
 */
export declare function $delete(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, options?: DataTypesDeleteOptionalParams): PollerLike<OperationState<void>, void>;
export declare function _deleteDataSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, body: Record<string, any>, options?: DataTypesDeleteDataOptionalParams): StreamableMethod<DataTypesDeleteData202Response | DataTypesDeleteData204Response | DataTypesDeleteDataDefaultResponse | DataTypesDeleteDataLogicalResponse>;
export declare function _deleteDataDeserialize(result: DataTypesDeleteData202Response | DataTypesDeleteData204Response | DataTypesDeleteDataDefaultResponse | DataTypesDeleteDataLogicalResponse): Promise<void>;
/** Delete data for data type. */
export declare function deleteData(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, body: Record<string, any>, options?: DataTypesDeleteDataOptionalParams): PollerLike<OperationState<void>, void>;
export declare function _generateStorageContainerSasTokenSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, body: ContainerSaS, options?: DataTypesGenerateStorageContainerSasTokenOptionalParams): StreamableMethod<DataTypesGenerateStorageContainerSasToken200Response | DataTypesGenerateStorageContainerSasTokenDefaultResponse>;
export declare function _generateStorageContainerSasTokenDeserialize(result: DataTypesGenerateStorageContainerSasToken200Response | DataTypesGenerateStorageContainerSasTokenDefaultResponse): Promise<ContainerSasToken>;
/** Generate sas token for storage container. */
export declare function generateStorageContainerSasToken(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, dataTypeName: string, body: ContainerSaS, options?: DataTypesGenerateStorageContainerSasTokenOptionalParams): Promise<ContainerSasToken>;
export declare function _listByDataProductSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataTypesListByDataProductOptionalParams): StreamableMethod<DataTypesListByDataProduct200Response | DataTypesListByDataProductDefaultResponse>;
export declare function _listByDataProductDeserialize(result: DataTypesListByDataProduct200Response | DataTypesListByDataProductDefaultResponse): Promise<DataTypeListResult>;
/** List data type by parent resource. */
export declare function listByDataProduct(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataTypesListByDataProductOptionalParams): PagedAsyncIterableIterator<DataType>;
//# sourceMappingURL=index.d.ts.map