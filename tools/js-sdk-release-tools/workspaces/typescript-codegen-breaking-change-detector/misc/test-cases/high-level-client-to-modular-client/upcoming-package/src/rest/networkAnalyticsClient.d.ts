import { ClientOptions } from "@azure-rest/core-client";
import { TokenCredential } from "@azure/core-auth";
import { NetworkAnalyticsContext } from "./clientDefinitions.js";
/**
 * Initialize a new instance of `NetworkAnalyticsContext`
 * @param credentials - uniquely identify client credential
 * @param options - the parameter for all optional parameters
 */
export default function createClient(credentials: TokenCredential, options?: ClientOptions): NetworkAnalyticsContext;
//# sourceMappingURL=networkAnalyticsClient.d.ts.map