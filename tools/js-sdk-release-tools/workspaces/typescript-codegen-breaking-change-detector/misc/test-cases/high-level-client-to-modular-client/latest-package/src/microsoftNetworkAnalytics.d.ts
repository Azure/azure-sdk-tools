import * as coreClient from "@azure/core-client";
import * as coreAuth from "@azure/core-auth";
import { Operations, DataProducts, DataProductsCatalogs, DataTypes } from "./operationsInterfaces";
import { MicrosoftNetworkAnalyticsOptionalParams } from "./models";
export declare class MicrosoftNetworkAnalytics extends coreClient.ServiceClient {
    $host: string;
    apiVersion: string;
    subscriptionId: string;
    /**
     * Initializes a new instance of the MicrosoftNetworkAnalytics class.
     * @param credentials Subscription credentials which uniquely identify client subscription.
     * @param subscriptionId The ID of the target subscription.
     * @param options The parameter options
     */
    constructor(credentials: coreAuth.TokenCredential, subscriptionId: string, options?: MicrosoftNetworkAnalyticsOptionalParams);
    /** A function that adds a policy that sets the api-version (or equivalent) to reflect the library version. */
    private addCustomApiVersionPolicy;
    operations: Operations;
    dataProducts: DataProducts;
    dataProductsCatalogs: DataProductsCatalogs;
    dataTypes: DataTypes;
}
//# sourceMappingURL=microsoftNetworkAnalytics.d.ts.map