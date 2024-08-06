// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
import { buildPagedAsyncIterator } from "../pagingHelpers.js";
import { isUnexpected, } from "../../rest/index.js";
import { operationOptionsToRequestParameters, createRestError, } from "@azure-rest/core-client";
export function _getSend(context, subscriptionId, resourceGroupName, options = { requestOptions: {} }) {
    return context
        .path("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.NetworkAnalytics/dataProductsCatalogs/default", subscriptionId, resourceGroupName)
        .get({ ...operationOptionsToRequestParameters(options) });
}
export async function _getDeserialize(result) {
    if (isUnexpected(result)) {
        throw createRestError(result);
    }
    return {
        id: result.body["id"],
        name: result.body["name"],
        type: result.body["type"],
        systemData: !result.body.systemData
            ? undefined
            : {
                createdBy: result.body.systemData?.["createdBy"],
                createdByType: result.body.systemData?.["createdByType"],
                createdAt: result.body.systemData?.["createdAt"] !== undefined
                    ? new Date(result.body.systemData?.["createdAt"])
                    : undefined,
                lastModifiedBy: result.body.systemData?.["lastModifiedBy"],
                lastModifiedByType: result.body.systemData?.["lastModifiedByType"],
                lastModifiedAt: result.body.systemData?.["lastModifiedAt"] !== undefined
                    ? new Date(result.body.systemData?.["lastModifiedAt"])
                    : undefined,
            },
        properties: !result.body.properties
            ? undefined
            : {
                provisioningState: result.body.properties?.["provisioningState"],
                publishers: result.body.properties?.["publishers"].map((p) => ({
                    publisherName: p["publisherName"],
                    dataProducts: p["dataProducts"].map((p) => ({
                        dataProductName: p["dataProductName"],
                        description: p["description"],
                        dataProductVersions: p["dataProductVersions"].map((p) => ({
                            version: p["version"],
                        })),
                    })),
                })),
            },
    };
}
/** Retrieve data type resource. */
export async function get(context, subscriptionId, resourceGroupName, options = { requestOptions: {} }) {
    const result = await _getSend(context, subscriptionId, resourceGroupName, options);
    return _getDeserialize(result);
}
export function _listByResourceGroupSend(context, subscriptionId, resourceGroupName, options = {
    requestOptions: {},
}) {
    return context
        .path("/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.NetworkAnalytics/dataProductsCatalogs", subscriptionId, resourceGroupName)
        .get({ ...operationOptionsToRequestParameters(options) });
}
export async function _listByResourceGroupDeserialize(result) {
    if (isUnexpected(result)) {
        throw createRestError(result);
    }
    return {
        value: result.body["value"].map((p) => ({
            id: p["id"],
            name: p["name"],
            type: p["type"],
            systemData: !p.systemData
                ? undefined
                : {
                    createdBy: p.systemData?.["createdBy"],
                    createdByType: p.systemData?.["createdByType"],
                    createdAt: p.systemData?.["createdAt"] !== undefined
                        ? new Date(p.systemData?.["createdAt"])
                        : undefined,
                    lastModifiedBy: p.systemData?.["lastModifiedBy"],
                    lastModifiedByType: p.systemData?.["lastModifiedByType"],
                    lastModifiedAt: p.systemData?.["lastModifiedAt"] !== undefined
                        ? new Date(p.systemData?.["lastModifiedAt"])
                        : undefined,
                },
            properties: !p.properties
                ? undefined
                : {
                    provisioningState: p.properties?.["provisioningState"],
                    publishers: p.properties?.["publishers"].map((p) => ({
                        publisherName: p["publisherName"],
                        dataProducts: p["dataProducts"].map((p) => ({
                            dataProductName: p["dataProductName"],
                            description: p["description"],
                            dataProductVersions: p["dataProductVersions"].map((p) => ({
                                version: p["version"],
                            })),
                        })),
                    })),
                },
        })),
        nextLink: result.body["nextLink"],
    };
}
/** List data catalog by resource group. */
export function listByResourceGroup(context, subscriptionId, resourceGroupName, options = {
    requestOptions: {},
}) {
    return buildPagedAsyncIterator(context, () => _listByResourceGroupSend(context, subscriptionId, resourceGroupName, options), _listByResourceGroupDeserialize, { itemName: "value", nextLinkName: "nextLink" });
}
export function _listBySubscriptionSend(context, subscriptionId, options = {
    requestOptions: {},
}) {
    return context
        .path("/subscriptions/{subscriptionId}/providers/Microsoft.NetworkAnalytics/dataProductsCatalogs", subscriptionId)
        .get({ ...operationOptionsToRequestParameters(options) });
}
export async function _listBySubscriptionDeserialize(result) {
    if (isUnexpected(result)) {
        throw createRestError(result);
    }
    return {
        value: result.body["value"].map((p) => ({
            id: p["id"],
            name: p["name"],
            type: p["type"],
            systemData: !p.systemData
                ? undefined
                : {
                    createdBy: p.systemData?.["createdBy"],
                    createdByType: p.systemData?.["createdByType"],
                    createdAt: p.systemData?.["createdAt"] !== undefined
                        ? new Date(p.systemData?.["createdAt"])
                        : undefined,
                    lastModifiedBy: p.systemData?.["lastModifiedBy"],
                    lastModifiedByType: p.systemData?.["lastModifiedByType"],
                    lastModifiedAt: p.systemData?.["lastModifiedAt"] !== undefined
                        ? new Date(p.systemData?.["lastModifiedAt"])
                        : undefined,
                },
            properties: !p.properties
                ? undefined
                : {
                    provisioningState: p.properties?.["provisioningState"],
                    publishers: p.properties?.["publishers"].map((p) => ({
                        publisherName: p["publisherName"],
                        dataProducts: p["dataProducts"].map((p) => ({
                            dataProductName: p["dataProductName"],
                            description: p["description"],
                            dataProductVersions: p["dataProductVersions"].map((p) => ({
                                version: p["version"],
                            })),
                        })),
                    })),
                },
        })),
        nextLink: result.body["nextLink"],
    };
}
/** List data catalog by subscription. */
export function listBySubscription(context, subscriptionId, options = {
    requestOptions: {},
}) {
    return buildPagedAsyncIterator(context, () => _listBySubscriptionSend(context, subscriptionId, options), _listBySubscriptionDeserialize, { itemName: "value", nextLinkName: "nextLink" });
}
//# sourceMappingURL=index.js.map