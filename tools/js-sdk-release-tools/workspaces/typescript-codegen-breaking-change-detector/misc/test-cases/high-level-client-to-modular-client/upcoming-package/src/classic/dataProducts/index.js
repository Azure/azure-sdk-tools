// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
import { create, get, update, $delete, generateStorageAccountSasToken, rotateKey, addUserRole, removeUserRole, listRolesAssignments, listByResourceGroup, listBySubscription, } from "../../api/dataProducts/index.js";
export function getDataProducts(context) {
    return {
        create: (subscriptionId, resourceGroupName, dataProductName, resource, options) => create(context, subscriptionId, resourceGroupName, dataProductName, resource, options),
        get: (subscriptionId, resourceGroupName, dataProductName, options) => get(context, subscriptionId, resourceGroupName, dataProductName, options),
        update: (subscriptionId, resourceGroupName, dataProductName, properties, options) => update(context, subscriptionId, resourceGroupName, dataProductName, properties, options),
        delete: (subscriptionId, resourceGroupName, dataProductName, options) => $delete(context, subscriptionId, resourceGroupName, dataProductName, options),
        generateStorageAccountSasToken: (subscriptionId, resourceGroupName, dataProductName, body, options) => generateStorageAccountSasToken(context, subscriptionId, resourceGroupName, dataProductName, body, options),
        rotateKey: (subscriptionId, resourceGroupName, dataProductName, body, options) => rotateKey(context, subscriptionId, resourceGroupName, dataProductName, body, options),
        addUserRole: (subscriptionId, resourceGroupName, dataProductName, body, options) => addUserRole(context, subscriptionId, resourceGroupName, dataProductName, body, options),
        removeUserRole: (subscriptionId, resourceGroupName, dataProductName, body, options) => removeUserRole(context, subscriptionId, resourceGroupName, dataProductName, body, options),
        listRolesAssignments: (subscriptionId, resourceGroupName, dataProductName, body, options) => listRolesAssignments(context, subscriptionId, resourceGroupName, dataProductName, body, options),
        listByResourceGroup: (subscriptionId, resourceGroupName, options) => listByResourceGroup(context, subscriptionId, resourceGroupName, options),
        listBySubscription: (subscriptionId, options) => listBySubscription(context, subscriptionId, options),
    };
}
export function getDataProductsOperations(context) {
    return {
        ...getDataProducts(context),
    };
}
//# sourceMappingURL=index.js.map