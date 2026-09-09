// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Reflection;
using Azure.Identity;
using Azure.Sdk.Tools.Cli.Services;

namespace Azure.Sdk.Tools.Cli.Tests.Services;

[TestFixture]
public class AzureServiceTests
{
    /// <summary>
    /// AzureService wraps ManagedIdentityCredential in a managed-identity-only DefaultAzureCredential (DAC) so
    /// the managed-identity probe fast-fails off-Azure instead of hanging. The tradeoff of DAC (vs. a hand-built
    /// ChainedTokenCredential) is that when Azure.Identity adds a new credential type, DAC includes it by default,
    /// which could silently pick up an unwanted identity from the environment. This test reflects over every
    /// Exclude*Credential option DAC exposes and fails when a new one is not excluded, forcing whoever bumps the
    /// Azure.Identity package to consciously exclude the new credential in AzureService.ManagedIdentityCredentialOptions.
    /// </summary>
    [Test]
    public void ManagedIdentityCredentialOptions_ExcludesEveryCredentialExceptManagedIdentity()
    {
        var options = AzureService.ManagedIdentityCredentialOptions;

        var excludeProperties = typeof(DefaultAzureCredentialOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool)
                        && p.CanRead
                        && p.CanWrite
                        && p.Name.StartsWith("Exclude", StringComparison.Ordinal)
                        && p.Name.EndsWith("Credential", StringComparison.Ordinal)
                        // Obsolete credentials are no longer probed by DefaultAzureCredential, so they are not a risk.
                        && p.GetCustomAttribute<ObsoleteAttribute>() is null)
            .ToList();

        Assert.That(excludeProperties, Is.Not.Empty,
            "Expected DefaultAzureCredentialOptions to expose Exclude*Credential properties; the reflection filter may be wrong.");

        Assert.Multiple(() =>
        {
            foreach (var property in excludeProperties)
            {
                var isExcluded = (bool)property.GetValue(options)!;

                if (property.Name == nameof(DefaultAzureCredentialOptions.ExcludeManagedIdentityCredential))
                {
                    Assert.That(isExcluded, Is.False,
                        $"{property.Name} must be false so the managed identity credential is the one credential DefaultAzureCredential probes.");
                }
                else
                {
                    Assert.That(isExcluded, Is.True,
                        $"{property.Name} is not excluded in AzureService.ManagedIdentityCredentialOptions. " +
                        "A new credential type was likely added to DefaultAzureCredential; set this option to true (or, if it is the intended identity, update this test) " +
                        "so the managed-identity-only credential does not silently pick up an unwanted identity from the environment.");
                }
            }
        });
    }
}
