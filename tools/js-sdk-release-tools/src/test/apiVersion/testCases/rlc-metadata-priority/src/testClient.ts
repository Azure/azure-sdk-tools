// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import { getClient, ClientOptions } from "@azure-rest/core-client";
import { TokenCredential } from "@azure/core-auth";

export interface TestClientOptions extends ClientOptions {
  apiVersion?: string;
}

export default function createClient(
  credentials: TokenCredential,
  {
    apiVersion = "2024-03-01-preview",
    ...options
  }: TestClientOptions = {},
): any {
  const endpointUrl = options.endpoint ?? options.baseUrl ?? `https://management.azure.com`;
  return getClient(endpointUrl, credentials, options);
}
