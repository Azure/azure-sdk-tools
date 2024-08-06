import { PollerLike, OperationState } from "@azure/core-lro";
import { DataProduct, DataProductUpdate, AccountSas, AccountSasToken, KeyVaultInfo, RoleAssignmentCommonProperties, RoleAssignmentDetail, ListRoleAssignments, DataProductListResult } from "../../models/models.js";
import { PagedAsyncIterableIterator } from "../../models/pagingTypes.js";
import { DataProductsAddUserRole200Response, DataProductsAddUserRoleDefaultResponse, DataProductsCreate200Response, DataProductsCreate201Response, DataProductsCreateDefaultResponse, DataProductsCreateLogicalResponse, DataProductsDelete202Response, DataProductsDelete204Response, DataProductsDeleteDefaultResponse, DataProductsDeleteLogicalResponse, DataProductsGenerateStorageAccountSasToken200Response, DataProductsGenerateStorageAccountSasTokenDefaultResponse, DataProductsGet200Response, DataProductsGetDefaultResponse, DataProductsListByResourceGroup200Response, DataProductsListByResourceGroupDefaultResponse, DataProductsListBySubscription200Response, DataProductsListBySubscriptionDefaultResponse, DataProductsListRolesAssignments200Response, DataProductsListRolesAssignmentsDefaultResponse, DataProductsRemoveUserRole204Response, DataProductsRemoveUserRoleDefaultResponse, DataProductsRotateKey204Response, DataProductsRotateKeyDefaultResponse, DataProductsUpdate200Response, DataProductsUpdate202Response, DataProductsUpdateDefaultResponse, DataProductsUpdateLogicalResponse, NetworkAnalyticsContext as Client } from "../../rest/index.js";
import { StreamableMethod } from "@azure-rest/core-client";
import { DataProductsCreateOptionalParams, DataProductsGetOptionalParams, DataProductsUpdateOptionalParams, DataProductsDeleteOptionalParams, DataProductsGenerateStorageAccountSasTokenOptionalParams, DataProductsRotateKeyOptionalParams, DataProductsAddUserRoleOptionalParams, DataProductsRemoveUserRoleOptionalParams, DataProductsListRolesAssignmentsOptionalParams, DataProductsListByResourceGroupOptionalParams, DataProductsListBySubscriptionOptionalParams } from "../../models/options.js";
export declare function _createSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, resource: DataProduct, options?: DataProductsCreateOptionalParams): StreamableMethod<DataProductsCreate200Response | DataProductsCreate201Response | DataProductsCreateDefaultResponse | DataProductsCreateLogicalResponse>;
export declare function _createDeserialize(result: DataProductsCreate200Response | DataProductsCreate201Response | DataProductsCreateDefaultResponse | DataProductsCreateLogicalResponse): Promise<DataProduct>;
/** Create data product resource. */
export declare function create(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, resource: DataProduct, options?: DataProductsCreateOptionalParams): PollerLike<OperationState<DataProduct>, DataProduct>;
export declare function _getSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataProductsGetOptionalParams): StreamableMethod<DataProductsGet200Response | DataProductsGetDefaultResponse>;
export declare function _getDeserialize(result: DataProductsGet200Response | DataProductsGetDefaultResponse): Promise<DataProduct>;
/** Retrieve data product resource. */
export declare function get(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataProductsGetOptionalParams): Promise<DataProduct>;
export declare function _updateSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, properties: DataProductUpdate, options?: DataProductsUpdateOptionalParams): StreamableMethod<DataProductsUpdate200Response | DataProductsUpdate202Response | DataProductsUpdateDefaultResponse | DataProductsUpdateLogicalResponse>;
export declare function _updateDeserialize(result: DataProductsUpdate200Response | DataProductsUpdate202Response | DataProductsUpdateDefaultResponse | DataProductsUpdateLogicalResponse): Promise<DataProduct>;
/** Update data product resource. */
export declare function update(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, properties: DataProductUpdate, options?: DataProductsUpdateOptionalParams): PollerLike<OperationState<DataProduct>, DataProduct>;
export declare function _$deleteSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataProductsDeleteOptionalParams): StreamableMethod<DataProductsDelete202Response | DataProductsDelete204Response | DataProductsDeleteDefaultResponse | DataProductsDeleteLogicalResponse>;
export declare function _$deleteDeserialize(result: DataProductsDelete202Response | DataProductsDelete204Response | DataProductsDeleteDefaultResponse | DataProductsDeleteLogicalResponse): Promise<void>;
/** Delete data product resource. */
/**
 *  @fixme delete is a reserved word that cannot be used as an operation name.
 *         Please add @clientName("clientName") or @clientName("<JS-Specific-Name>", "javascript")
 *         to the operation to override the generated name.
 */
export declare function $delete(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataProductsDeleteOptionalParams): PollerLike<OperationState<void>, void>;
export declare function _generateStorageAccountSasTokenSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: AccountSas, options?: DataProductsGenerateStorageAccountSasTokenOptionalParams): StreamableMethod<DataProductsGenerateStorageAccountSasToken200Response | DataProductsGenerateStorageAccountSasTokenDefaultResponse>;
export declare function _generateStorageAccountSasTokenDeserialize(result: DataProductsGenerateStorageAccountSasToken200Response | DataProductsGenerateStorageAccountSasTokenDefaultResponse): Promise<AccountSasToken>;
/** Generate sas token for storage account. */
export declare function generateStorageAccountSasToken(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: AccountSas, options?: DataProductsGenerateStorageAccountSasTokenOptionalParams): Promise<AccountSasToken>;
export declare function _rotateKeySend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: KeyVaultInfo, options?: DataProductsRotateKeyOptionalParams): StreamableMethod<DataProductsRotateKey204Response | DataProductsRotateKeyDefaultResponse>;
export declare function _rotateKeyDeserialize(result: DataProductsRotateKey204Response | DataProductsRotateKeyDefaultResponse): Promise<void>;
/** Initiate key rotation on Data Product. */
export declare function rotateKey(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: KeyVaultInfo, options?: DataProductsRotateKeyOptionalParams): Promise<void>;
export declare function _addUserRoleSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: RoleAssignmentCommonProperties, options?: DataProductsAddUserRoleOptionalParams): StreamableMethod<DataProductsAddUserRole200Response | DataProductsAddUserRoleDefaultResponse>;
export declare function _addUserRoleDeserialize(result: DataProductsAddUserRole200Response | DataProductsAddUserRoleDefaultResponse): Promise<RoleAssignmentDetail>;
/** Assign role to the data product. */
export declare function addUserRole(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: RoleAssignmentCommonProperties, options?: DataProductsAddUserRoleOptionalParams): Promise<RoleAssignmentDetail>;
export declare function _removeUserRoleSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: RoleAssignmentDetail, options?: DataProductsRemoveUserRoleOptionalParams): StreamableMethod<DataProductsRemoveUserRole204Response | DataProductsRemoveUserRoleDefaultResponse>;
export declare function _removeUserRoleDeserialize(result: DataProductsRemoveUserRole204Response | DataProductsRemoveUserRoleDefaultResponse): Promise<void>;
/** Remove role from the data product. */
export declare function removeUserRole(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: RoleAssignmentDetail, options?: DataProductsRemoveUserRoleOptionalParams): Promise<void>;
export declare function _listRolesAssignmentsSend(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: Record<string, any>, options?: DataProductsListRolesAssignmentsOptionalParams): StreamableMethod<DataProductsListRolesAssignments200Response | DataProductsListRolesAssignmentsDefaultResponse>;
export declare function _listRolesAssignmentsDeserialize(result: DataProductsListRolesAssignments200Response | DataProductsListRolesAssignmentsDefaultResponse): Promise<ListRoleAssignments>;
/** List user roles associated with the data product. */
export declare function listRolesAssignments(context: Client, subscriptionId: string, resourceGroupName: string, dataProductName: string, body: Record<string, any>, options?: DataProductsListRolesAssignmentsOptionalParams): Promise<ListRoleAssignments>;
export declare function _listByResourceGroupSend(context: Client, subscriptionId: string, resourceGroupName: string, options?: DataProductsListByResourceGroupOptionalParams): StreamableMethod<DataProductsListByResourceGroup200Response | DataProductsListByResourceGroupDefaultResponse>;
export declare function _listByResourceGroupDeserialize(result: DataProductsListByResourceGroup200Response | DataProductsListByResourceGroupDefaultResponse): Promise<DataProductListResult>;
/** List data products by resource group. */
export declare function listByResourceGroup(context: Client, subscriptionId: string, resourceGroupName: string, options?: DataProductsListByResourceGroupOptionalParams): PagedAsyncIterableIterator<DataProduct>;
export declare function _listBySubscriptionSend(context: Client, subscriptionId: string, options?: DataProductsListBySubscriptionOptionalParams): StreamableMethod<DataProductsListBySubscription200Response | DataProductsListBySubscriptionDefaultResponse>;
export declare function _listBySubscriptionDeserialize(result: DataProductsListBySubscription200Response | DataProductsListBySubscriptionDefaultResponse): Promise<DataProductListResult>;
/** List data products by subscription. */
export declare function listBySubscription(context: Client, subscriptionId: string, options?: DataProductsListBySubscriptionOptionalParams): PagedAsyncIterableIterator<DataProduct>;
//# sourceMappingURL=index.d.ts.map