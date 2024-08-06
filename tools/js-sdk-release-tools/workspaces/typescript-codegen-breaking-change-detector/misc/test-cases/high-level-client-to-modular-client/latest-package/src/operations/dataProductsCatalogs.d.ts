import { PagedAsyncIterableIterator } from "@azure/core-paging";
import { DataProductsCatalogs } from "../operationsInterfaces";
import { MicrosoftNetworkAnalytics } from "../microsoftNetworkAnalytics";
import { DataProductsCatalog, DataProductsCatalogsListBySubscriptionOptionalParams, DataProductsCatalogsListByResourceGroupOptionalParams, DataProductsCatalogsGetOptionalParams, DataProductsCatalogsGetResponse } from "../models";
/** Class containing DataProductsCatalogs operations. */
export declare class DataProductsCatalogsImpl implements DataProductsCatalogs {
    private readonly client;
    /**
     * Initialize a new instance of the class DataProductsCatalogs class.
     * @param client Reference to the service client
     */
    constructor(client: MicrosoftNetworkAnalytics);
    /**
     * List data catalog by subscription.
     * @param options The options parameters.
     */
    listBySubscription(options?: DataProductsCatalogsListBySubscriptionOptionalParams): PagedAsyncIterableIterator<DataProductsCatalog>;
    private listBySubscriptionPagingPage;
    private listBySubscriptionPagingAll;
    /**
     * List data catalog by resource group.
     * @param resourceGroupName The name of the resource group. The name is case insensitive.
     * @param options The options parameters.
     */
    listByResourceGroup(resourceGroupName: string, options?: DataProductsCatalogsListByResourceGroupOptionalParams): PagedAsyncIterableIterator<DataProductsCatalog>;
    private listByResourceGroupPagingPage;
    private listByResourceGroupPagingAll;
    /**
     * List data catalog by subscription.
     * @param options The options parameters.
     */
    private _listBySubscription;
    /**
     * List data catalog by resource group.
     * @param resourceGroupName The name of the resource group. The name is case insensitive.
     * @param options The options parameters.
     */
    private _listByResourceGroup;
    /**
     * Retrieve data type resource.
     * @param resourceGroupName The name of the resource group. The name is case insensitive.
     * @param options The options parameters.
     */
    get(resourceGroupName: string, options?: DataProductsCatalogsGetOptionalParams): Promise<DataProductsCatalogsGetResponse>;
    /**
     * ListBySubscriptionNext
     * @param nextLink The nextLink from the previous successful call to the ListBySubscription method.
     * @param options The options parameters.
     */
    private _listBySubscriptionNext;
    /**
     * ListByResourceGroupNext
     * @param resourceGroupName The name of the resource group. The name is case insensitive.
     * @param nextLink The nextLink from the previous successful call to the ListByResourceGroup method.
     * @param options The options parameters.
     */
    private _listByResourceGroupNext;
}
//# sourceMappingURL=dataProductsCatalogs.d.ts.map