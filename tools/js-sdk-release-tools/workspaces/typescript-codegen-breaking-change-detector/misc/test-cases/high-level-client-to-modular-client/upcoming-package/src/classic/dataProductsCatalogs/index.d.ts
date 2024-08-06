import { NetworkAnalyticsContext } from "../../api/networkAnalyticsContext.js";
import { DataProductsCatalog } from "../../models/models.js";
import { PagedAsyncIterableIterator } from "../../models/pagingTypes.js";
import { DataProductsCatalogsGetOptionalParams, DataProductsCatalogsListByResourceGroupOptionalParams, DataProductsCatalogsListBySubscriptionOptionalParams } from "../../models/options.js";
export interface DataProductsCatalogsOperations {
    get: (subscriptionId: string, resourceGroupName: string, options?: DataProductsCatalogsGetOptionalParams) => Promise<DataProductsCatalog>;
    listByResourceGroup: (subscriptionId: string, resourceGroupName: string, options?: DataProductsCatalogsListByResourceGroupOptionalParams) => PagedAsyncIterableIterator<DataProductsCatalog>;
    listBySubscription: (subscriptionId: string, options?: DataProductsCatalogsListBySubscriptionOptionalParams) => PagedAsyncIterableIterator<DataProductsCatalog>;
}
export declare function getDataProductsCatalogs(context: NetworkAnalyticsContext): {
    get: (subscriptionId: string, resourceGroupName: string, options?: DataProductsCatalogsGetOptionalParams) => Promise<DataProductsCatalog>;
    listByResourceGroup: (subscriptionId: string, resourceGroupName: string, options?: DataProductsCatalogsListByResourceGroupOptionalParams) => PagedAsyncIterableIterator<DataProductsCatalog, DataProductsCatalog[], import("../../models/pagingTypes.js").PageSettings>;
    listBySubscription: (subscriptionId: string, options?: DataProductsCatalogsListBySubscriptionOptionalParams) => PagedAsyncIterableIterator<DataProductsCatalog, DataProductsCatalog[], import("../../models/pagingTypes.js").PageSettings>;
};
export declare function getDataProductsCatalogsOperations(context: NetworkAnalyticsContext): DataProductsCatalogsOperations;
//# sourceMappingURL=index.d.ts.map