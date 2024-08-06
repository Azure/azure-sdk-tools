// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
import { get, listByResourceGroup, listBySubscription, } from "../../api/dataProductsCatalogs/index.js";
export function getDataProductsCatalogs(context) {
    return {
        get: (subscriptionId, resourceGroupName, options) => get(context, subscriptionId, resourceGroupName, options),
        listByResourceGroup: (subscriptionId, resourceGroupName, options) => listByResourceGroup(context, subscriptionId, resourceGroupName, options),
        listBySubscription: (subscriptionId, options) => listBySubscription(context, subscriptionId, options),
    };
}
export function getDataProductsCatalogsOperations(context) {
    return {
        ...getDataProductsCatalogs(context),
    };
}
//# sourceMappingURL=index.js.map