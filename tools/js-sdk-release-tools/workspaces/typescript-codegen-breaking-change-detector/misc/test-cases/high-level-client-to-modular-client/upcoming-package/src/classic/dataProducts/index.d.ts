import { NetworkAnalyticsContext } from "../../api/networkAnalyticsContext.js";
import { DataProduct, DataProductUpdate, AccountSas, AccountSasToken, KeyVaultInfo, RoleAssignmentCommonProperties, RoleAssignmentDetail, ListRoleAssignments } from "../../models/models.js";
import { PagedAsyncIterableIterator } from "../../models/pagingTypes.js";
import { PollerLike, OperationState } from "@azure/core-lro";
import { DataProductsCreateOptionalParams, DataProductsGetOptionalParams, DataProductsUpdateOptionalParams, DataProductsDeleteOptionalParams, DataProductsGenerateStorageAccountSasTokenOptionalParams, DataProductsRotateKeyOptionalParams, DataProductsAddUserRoleOptionalParams, DataProductsRemoveUserRoleOptionalParams, DataProductsListRolesAssignmentsOptionalParams, DataProductsListByResourceGroupOptionalParams, DataProductsListBySubscriptionOptionalParams } from "../../models/options.js";
export interface DataProductsOperations {
    create: (subscriptionId: string, resourceGroupName: string, dataProductName: string, resource: DataProduct, options?: DataProductsCreateOptionalParams) => PollerLike<OperationState<DataProduct>, DataProduct>;
    get: (subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataProductsGetOptionalParams) => Promise<DataProduct>;
    update: (subscriptionId: string, resourceGroupName: string, dataProductName: string, properties: DataProductUpdate, options?: DataProductsUpdateOptionalParams) => PollerLike<OperationState<DataProduct>, DataProduct>;
    delete: (subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataProductsDeleteOptionalParams) => PollerLike<OperationState<void>, void>;
    generateStorageAccountSasToken: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: AccountSas, options?: DataProductsGenerateStorageAccountSasTokenOptionalParams) => Promise<AccountSasToken>;
    rotateKey: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: KeyVaultInfo, options?: DataProductsRotateKeyOptionalParams) => Promise<void>;
    addUserRole: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: RoleAssignmentCommonProperties, options?: DataProductsAddUserRoleOptionalParams) => Promise<RoleAssignmentDetail>;
    removeUserRole: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: RoleAssignmentDetail, options?: DataProductsRemoveUserRoleOptionalParams) => Promise<void>;
    listRolesAssignments: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: Record<string, any>, options?: DataProductsListRolesAssignmentsOptionalParams) => Promise<ListRoleAssignments>;
    listByResourceGroup: (subscriptionId: string, resourceGroupName: string, options?: DataProductsListByResourceGroupOptionalParams) => PagedAsyncIterableIterator<DataProduct>;
    listBySubscription: (subscriptionId: string, options?: DataProductsListBySubscriptionOptionalParams) => PagedAsyncIterableIterator<DataProduct>;
}
export declare function getDataProducts(context: NetworkAnalyticsContext): {
    create: (subscriptionId: string, resourceGroupName: string, dataProductName: string, resource: DataProduct, options?: DataProductsCreateOptionalParams) => PollerLike<OperationState<DataProduct>, DataProduct>;
    get: (subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataProductsGetOptionalParams) => Promise<DataProduct>;
    update: (subscriptionId: string, resourceGroupName: string, dataProductName: string, properties: DataProductUpdate, options?: DataProductsUpdateOptionalParams) => PollerLike<OperationState<DataProduct>, DataProduct>;
    delete: (subscriptionId: string, resourceGroupName: string, dataProductName: string, options?: DataProductsDeleteOptionalParams) => PollerLike<OperationState<void>, void>;
    generateStorageAccountSasToken: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: AccountSas, options?: DataProductsGenerateStorageAccountSasTokenOptionalParams) => Promise<AccountSasToken>;
    rotateKey: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: KeyVaultInfo, options?: DataProductsRotateKeyOptionalParams) => Promise<void>;
    addUserRole: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: RoleAssignmentCommonProperties, options?: DataProductsAddUserRoleOptionalParams) => Promise<RoleAssignmentDetail>;
    removeUserRole: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: RoleAssignmentDetail, options?: DataProductsRemoveUserRoleOptionalParams) => Promise<void>;
    listRolesAssignments: (subscriptionId: string, resourceGroupName: string, dataProductName: string, body: Record<string, any>, options?: DataProductsListRolesAssignmentsOptionalParams) => Promise<ListRoleAssignments>;
    listByResourceGroup: (subscriptionId: string, resourceGroupName: string, options?: DataProductsListByResourceGroupOptionalParams) => PagedAsyncIterableIterator<DataProduct, DataProduct[], import("../../models/pagingTypes.js").PageSettings>;
    listBySubscription: (subscriptionId: string, options?: DataProductsListBySubscriptionOptionalParams) => PagedAsyncIterableIterator<DataProduct, DataProduct[], import("../../models/pagingTypes.js").PageSettings>;
};
export declare function getDataProductsOperations(context: NetworkAnalyticsContext): DataProductsOperations;
//# sourceMappingURL=index.d.ts.map