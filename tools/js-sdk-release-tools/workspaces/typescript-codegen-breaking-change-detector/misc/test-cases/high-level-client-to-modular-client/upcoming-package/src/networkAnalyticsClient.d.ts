import { TokenCredential } from "@azure/core-auth";
import { Pipeline } from "@azure/core-rest-pipeline";
import { OperationsOperations } from "./classic/operations/index.js";
import { DataProductsCatalogsOperations } from "./classic/dataProductsCatalogs/index.js";
import { DataTypesOperations } from "./classic/dataTypes/index.js";
import { DataProductsOperations } from "./classic/dataProducts/index.js";
import { NetworkAnalyticsClientOptions } from "./api/index.js";
export { NetworkAnalyticsClientOptions } from "./api/networkAnalyticsContext.js";
export declare class NetworkAnalyticsClient {
    private _client;
    /** The pipeline used by this client to make requests */
    readonly pipeline: Pipeline;
    constructor(credential: TokenCredential, options?: NetworkAnalyticsClientOptions);
    /** The operation groups for Operations */
    readonly operations: OperationsOperations;
    /** The operation groups for DataProductsCatalogs */
    readonly dataProductsCatalogs: DataProductsCatalogsOperations;
    /** The operation groups for DataTypes */
    readonly dataTypes: DataTypesOperations;
    /** The operation groups for DataProducts */
    readonly dataProducts: DataProductsOperations;
}
//# sourceMappingURL=networkAnalyticsClient.d.ts.map