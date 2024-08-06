import { PagedAsyncIterableIterator } from "@azure/core-paging";
import { DataProductsCatalog, DataProductsCatalogsListBySubscriptionOptionalParams, DataProductsCatalogsListByResourceGroupOptionalParams, DataProductsCatalogsGetOptionalParams, DataProductsCatalogsGetResponse } from "../models";
/** Interface representing a DataProductsCatalogs. */
export interface DataProductsCatalogs {
    /**
     * List data catalog by subscription.
     * @param options The options parameters.
     */
    listBySubscription(options?: DataProductsCatalogsListBySubscriptionOptionalParams): PagedAsyncIterableIterator<DataProductsCatalog>;
    /**
     * List data catalog by resource group.
     * @param resourceGroupName The name of the resource group. The name is case insensitive.
     * @param options The options parameters.
     */
    listByResourceGroup(resourceGroupName: string, options?: DataProductsCatalogsListByResourceGroupOptionalParams): PagedAsyncIterableIterator<DataProductsCatalog>;
    /**
     * Retrieve data type resource.
     * @param resourceGroupName The name of the resource group. The name is case insensitive.
     * @param options The options parameters.
     */
    get(resourceGroupName: string, options?: DataProductsCatalogsGetOptionalParams): Promise<DataProductsCatalogsGetResponse>;
}
//# sourceMappingURL=dataProductsCatalogs.d.ts.map