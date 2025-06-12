using System.Text.RegularExpressions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Microsoft.FxLister.Services;

public static class PackageAnalyzer
{
    public static async Task<List<string>> DiscoverAzurePackagesAsync(int maxPackages = 100, string packagePattern = @"^Azure\.(?!ResourceManager)(?!Provisioning)")
    {
        var azurePackages = new List<string>();
        var regex = new Regex(packagePattern, RegexOptions.IgnoreCase);
        
        try
        {
            // Set up NuGet configuration
            var settings = Settings.LoadDefaultSettings(null);
            var sourceRepositoryProvider = new SourceRepositoryProvider(settings, Repository.Provider.GetCoreV3());
            
            // Get the official NuGet.org repository
            var nugetOrgSource = sourceRepositoryProvider.GetRepositories()
                .FirstOrDefault(r => r.PackageSource.Source.Contains("nuget.org"));
            
            if (nugetOrgSource == null)
            {
                // Fallback to creating nuget.org repository manually
                var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
                nugetOrgSource = new SourceRepository(packageSource, Repository.Provider.GetCoreV3());
            }
            
            var searchResource = await nugetOrgSource.GetResourceAsync<PackageSearchResource>();
            
            // Search for Azure packages
            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new[] { "netstandard2.0" }
            };
            
            int skip = 0;
            const int batchSize = 20; // Hardcoded batch size for pagination
            bool hasMore = true;
            
            while (hasMore && azurePackages.Count < maxPackages)
            {
                var searchResults = await searchResource.SearchAsync(
                    "Azure",
                    searchFilter,
                    skip,
                    batchSize,
                    NullLogger.Instance,
                    CancellationToken.None);
                
                var packages = searchResults.ToList();
                if (packages.Count == 0)
                {
                    hasMore = false;
                    break;
                }
                
                foreach (var package in packages)
                {
                    if (azurePackages.Count >= maxPackages)
                        break;
                        
                    var packageId = package.Identity.Id;
                    
                    // Filter packages using the regex pattern
                    if (regex.IsMatch(packageId))
                    {
                        azurePackages.Add(packageId);
                    }
                }
                
                skip += batchSize;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to discover Azure packages: {ex.Message}", ex);
        }
        
        return azurePackages.Distinct().ToList();
    }
}