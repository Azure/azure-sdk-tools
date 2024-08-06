// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
import { create, get, update, $delete, deleteData, generateStorageContainerSasToken, listByDataProduct, } from "../../api/dataTypes/index.js";
export function getDataTypes(context) {
    return {
        create: (subscriptionId, resourceGroupName, dataProductName, dataTypeName, resource, options) => create(context, subscriptionId, resourceGroupName, dataProductName, dataTypeName, resource, options),
        get: (subscriptionId, resourceGroupName, dataProductName, dataTypeName, options) => get(context, subscriptionId, resourceGroupName, dataProductName, dataTypeName, options),
        update: (subscriptionId, resourceGroupName, dataProductName, dataTypeName, properties, options) => update(context, subscriptionId, resourceGroupName, dataProductName, dataTypeName, properties, options),
        delete: (subscriptionId, resourceGroupName, dataProductName, dataTypeName, options) => $delete(context, subscriptionId, resourceGroupName, dataProductName, dataTypeName, options),
        deleteData: (subscriptionId, resourceGroupName, dataProductName, dataTypeName, body, options) => deleteData(context, subscriptionId, resourceGroupName, dataProductName, dataTypeName, body, options),
        generateStorageContainerSasToken: (subscriptionId, resourceGroupName, dataProductName, dataTypeName, body, options) => generateStorageContainerSasToken(context, subscriptionId, resourceGroupName, dataProductName, dataTypeName, body, options),
        listByDataProduct: (subscriptionId, resourceGroupName, dataProductName, options) => listByDataProduct(context, subscriptionId, resourceGroupName, dataProductName, options),
    };
}
export function getDataTypesOperations(context) {
    return {
        ...getDataTypes(context),
    };
}
//# sourceMappingURL=index.js.map