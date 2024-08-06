// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
import getClient from "../rest/index.js";
export function createNetworkAnalytics(credential, options = {}) {
    const clientContext = getClient(credential, options);
    return clientContext;
}
//# sourceMappingURL=networkAnalyticsContext.js.map