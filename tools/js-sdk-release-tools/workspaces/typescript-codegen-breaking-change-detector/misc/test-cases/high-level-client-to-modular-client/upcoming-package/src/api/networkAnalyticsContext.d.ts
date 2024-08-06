import { TokenCredential } from "@azure/core-auth";
import { ClientOptions } from "@azure-rest/core-client";
import { NetworkAnalyticsContext } from "../rest/index.js";
export interface NetworkAnalyticsClientOptions extends ClientOptions {
    /** The API version to use for this operation. */
    apiVersion?: string;
}
export { NetworkAnalyticsContext } from "../rest/index.js";
export declare function createNetworkAnalytics(credential: TokenCredential, options?: NetworkAnalyticsClientOptions): NetworkAnalyticsContext;
//# sourceMappingURL=networkAnalyticsContext.d.ts.map