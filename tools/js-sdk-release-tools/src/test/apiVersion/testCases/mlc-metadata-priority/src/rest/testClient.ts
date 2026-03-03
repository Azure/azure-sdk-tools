// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { getClient, ClientOptions } from "@azure-rest/core-client";
import { TokenCredential } from "@azure/core-auth";

/** The optional parameters for the client */
export interface TestContextOptions extends ClientOptions {
  /** The api version option of the client */
  apiVersion?: string;
}

/**
 * Initialize a new instance of `TestContext`
 * @param credentials - uniquely identify client credential
 * @param options - the parameter for all optional parameters
 */
export default function createClient(
  credentials: TokenCredential,
  {
    apiVersion = "2024-03-01-preview",
    ...options
  }: TestContextOptions = {},
): any {
  const endpointUrl = options.endpoint ?? options.baseUrl ?? `https://management.azure.com`;
  return getClient(endpointUrl, credentials, options);
}
