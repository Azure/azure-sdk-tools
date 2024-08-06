import { DataProductsCatalog, DataProductsCatalogListResult } from "../../models/models.js";
import { PagedAsyncIterableIterator } from "../../models/pagingTypes.js";
import { DataProductsCatalogsGet200Response, DataProductsCatalogsGetDefaultResponse, DataProductsCatalogsListByResourceGroup200Response, DataProductsCatalogsListByResourceGroupDefaultResponse, DataProductsCatalogsListBySubscription200Response, DataProductsCatalogsListBySubscriptionDefaultResponse, NetworkAnalyticsContext as Client } from "../../rest/index.js";
import { StreamableMethod } from "@azure-rest/core-client";
import { DataProductsCatalogsGetOptionalParams, DataProductsCatalogsListByResourceGroupOptionalParams, DataProductsCatalogsListBySubscriptionOptionalParams } from "../../models/options.js";
export declare function _getSend(context: Client, subscriptionId: string, resourceGroupName: string, options?: DataProductsCatalogsGetOptionalParams): StreamableMethod<DataProductsCatalogsGet200Response | DataProductsCatalogsGetDefaultResponse>;
export declare function _getDeserialize(result: DataProductsCatalogsGet200Response | DataProductsCatalogsGetDefaultResponse): Promise<DataProductsCatalog>;
/** Retrieve data type resource. */
export declare function get(context: Client, subscriptionId: string, resourceGroupName: string, options?: DataProductsCatalogsGetOptionalParams): Promise<DataProductsCatalog>;
export declare function _listByResourceGroupSend(context: Client, subscriptionId: string, resourceGroupName: string, options?: DataProductsCatalogsListByResourceGroupOptionalParams): StreamableMethod<DataProductsCatalogsListByResourceGroup200Response | DataProductsCatalogsListByResourceGroupDefaultResponse>;
export declare function _listByResourceGroupDeserialize(result: DataProductsCatalogsListByResourceGroup200Response | DataProductsCatalogsListByResourceGroupDefaultResponse): Promise<DataProductsCatalogListResult>;
/** List data catalog by resource group. */
export declare function listByResourceGroup(context: Client, subscriptionId: string, resourceGroupName: string, options?: DataProductsCatalogsListByResourceGroupOptionalParams): PagedAsyncIterableIterator<DataProductsCatalog>;
export declare function _listBySubscriptionSend(context: Client, subscriptionId: string, options?: DataProductsCatalogsListBySubscriptionOptionalParams): StreamableMethod<DataProductsCatalogsListBySubscription200Response | DataProductsCatalogsListBySubscriptionDefaultResponse>;
export declare function _listBySubscriptionDeserialize(result: DataProductsCatalogsListBySubscription200Response | DataProductsCatalogsListBySubscriptionDefaultResponse): Promise<DataProductsCatalogListResult>;
/** List data catalog by subscription. */
export declare function listBySubscription(context: Client, subscriptionId: string, options?: DataProductsCatalogsListBySubscriptionOptionalParams): PagedAsyncIterableIterator<DataProductsCatalog>;
//# sourceMappingURL=index.d.ts.map