// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Azure.Core;
using Azure.Identity;

namespace APIViewWeb.Helpers;

public static class CredentialProvider
{
    public static TokenCredential GetAzureCredential()
    {
        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                          Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        bool isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
        if (isDevelopment)
        {
            return new ChainedTokenCredential(
                new AzureCliCredential(),
                new AzureDeveloperCliCredential());
        }

        return new ManagedIdentityCredential();
    }
}
