using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Packaging;
using NuGet.Versioning;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.FxLister.Services;

public class RealTypeExtractor
{
    public RealTypeExtractor()
    {
    }
    
    public async Task<List<string>> ExtractTypesFromPackagesAsync(List<string> packageIds)
    {
        var allTypes = new HashSet<string>();
        
        try
        {
            foreach (var packageId in packageIds) // Process all packages
            {
                try
                {
                    Console.WriteLine($"Processing package: {packageId}");
                    
                    var types = await ExtractTypesFromSinglePackageAsync(packageId);
                    Console.WriteLine($"  Found {types.Count} types in {packageId}");
                    
                    foreach (var type in types)
                    {
                        allTypes.Add(type);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to process package {packageId}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to extract types: {ex.Message}", ex);
        }
        
        return allTypes.ToList();
    }
    
    private async Task<List<string>> ExtractTypesFromSinglePackageAsync(string packageId)
    {
        var types = new List<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), "FxLister", Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Use NuGet CLI approach - download package to temp directory
            var settings = Settings.LoadDefaultSettings(null);
            var packageSourceProvider = new PackageSourceProvider(settings);
            var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, Repository.Provider.GetCoreV3());
            
            var nugetOrgSource = sourceRepositoryProvider.GetRepositories().First();
            var findPackageByIdResource = await nugetOrgSource.GetResourceAsync<FindPackageByIdResource>();
            
            // Get available versions
            var versions = await findPackageByIdResource.GetAllVersionsAsync(
                packageId,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None);
            
            var latestVersion = versions.Where(v => !v.IsPrerelease).OrderByDescending(v => v).FirstOrDefault();
            if (latestVersion == null)
                return types;
            
            // Download the package
            var packagePath = Path.Combine(tempDir, $"{packageId}.{latestVersion}.nupkg");
            using var packageStream = File.Create(packagePath);
            
            var success = await findPackageByIdResource.CopyNupkgToStreamAsync(
                packageId,
                latestVersion,
                packageStream,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None);
            
            if (!success)
                return types;
            
            packageStream.Close();
            
            // Extract types from the downloaded package
            using var fileStream = File.OpenRead(packagePath);
            using var packageReader = new PackageArchiveReader(fileStream);
            
            types = ExtractTypesFromPackage(packageReader);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        return types;
    }
    
    private List<string> ExtractTypesFromPackage(PackageArchiveReader packageReader)
    {
        var types = new List<string>();
        
        try
        {
            var libItems = packageReader.GetLibItems();
            
            // Try to find assemblies for netstandard2.0
            var targetFrameworks = new[] { "netstandard2.0" };
            
            foreach (var tfm in targetFrameworks)
            {
                var compatibleLibItems = libItems.FirstOrDefault(lib => 
                    lib.TargetFramework.GetShortFolderName().Equals(tfm, StringComparison.OrdinalIgnoreCase));
                
                if (compatibleLibItems != null)
                {
                    foreach (var file in compatibleLibItems.Items.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            using var stream = packageReader.GetStream(file);
                            using var memoryStream = new MemoryStream();
                            stream.CopyTo(memoryStream);
                            memoryStream.Position = 0;
                            
                            var assemblyTypes = ExtractTypesFromAssembly(memoryStream);
                            types.AddRange(assemblyTypes);
                        }
                        catch
                        {
                            // Skip assemblies that can't be read
                        }
                    }
                    break; // Use the first compatible target framework found
                }
            }
        }
        catch
        {
            // Skip packages that can't be processed
        }
        
        return types;
    }
    
    private List<string> ExtractTypesFromAssembly(Stream assemblyStream)
    {
        var types = new List<string>();
        
        try
        {
            using var peReader = new PEReader(assemblyStream);
            var metadataReader = peReader.GetMetadataReader();
            
            foreach (var typeDefHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                
                // Only include public types
                if (!typeDef.Attributes.HasFlag(TypeAttributes.Public))
                    continue;
                
                var typeName = metadataReader.GetString(typeDef.Name);
                
                // Skip compiler-generated types and special types
                if (typeName.StartsWith("<") || 
                    typeName.Contains("__") || 
                    typeName.StartsWith("Program") ||
                    typeName.Equals("<Module>"))
                    continue;
                
                types.Add(typeName);
            }
        }
        catch
        {
            // Skip assemblies that can't be analyzed
        }
        
        return types;
    }
}