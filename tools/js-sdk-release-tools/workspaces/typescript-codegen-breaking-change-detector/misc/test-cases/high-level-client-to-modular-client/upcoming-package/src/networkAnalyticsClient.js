// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
import { getOperationsOperations, } from "./classic/operations/index.js";
import { getDataProductsCatalogsOperations, } from "./classic/dataProductsCatalogs/index.js";
import { getDataTypesOperations, } from "./classic/dataTypes/index.js";
import { getDataProductsOperations, } from "./classic/dataProducts/index.js";
import { createNetworkAnalytics, } from "./api/index.js";
export class NetworkAnalyticsClient {
    _client;
    /** The pipeline used by this client to make requests */
    pipeline;
    constructor(credential, options = {}) {
        this._client = createNetworkAnalytics(credential, options);
        this.pipeline = this._client.pipeline;
        this.operations = getOperationsOperations(this._client);
        this.dataProductsCatalogs = getDataProductsCatalogsOperations(this._client);
        this.dataTypes = getDataTypesOperations(this._client);
        this.dataProducts = getDataProductsOperations(this._client);
    }
    /** The operation groups for Operations */
    operations;
    /** The operation groups for DataProductsCatalogs */
    dataProductsCatalogs;
    /** The operation groups for DataTypes */
    dataTypes;
    /** The operation groups for DataProducts */
    dataProducts;
}
//# sourceMappingURL=networkAnalyticsClient.js.map