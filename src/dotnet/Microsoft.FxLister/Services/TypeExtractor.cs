using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.FxLister.Services;

public class TypeExtractor
{
    private readonly ILogger _logger;
    
    public TypeExtractor()
    {
        _logger = NullLogger.Instance;
    }
    
    public async Task<List<string>> ExtractTypesFromPackagesAsync(List<string> packageIds)
    {
        var allTypes = new HashSet<string>();
        var tempDir = Path.Combine(Path.GetTempPath(), "FxLister", Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            // Set up NuGet configuration
            var settings = Settings.LoadDefaultSettings(null);
            var sourceRepositoryProvider = new SourceRepositoryProvider(settings, Repository.Provider.GetCoreV3());
            
            var nugetOrgSource = sourceRepositoryProvider.GetRepositories()
                .FirstOrDefault(r => r.PackageSource.Source.Contains("nuget.org"));
            
            if (nugetOrgSource == null)
            {
                var packageSource = new PackageSource("https://api.nuget.org/v3/index.json");
                nugetOrgSource = new SourceRepository(packageSource, Repository.Provider.GetCoreV3());
            }
            
            var downloadResource = await nugetOrgSource.GetResourceAsync<DownloadResource>();
            var metadataResource = await nugetOrgSource.GetResourceAsync<PackageMetadataResource>();
            
            foreach (var packageId in packageIds)
            {
                try
                {
                    Console.WriteLine($"Processing package: {packageId}");
                    
                    // Get package metadata to find the latest stable version
                    var metadata = await metadataResource.GetMetadataAsync(
                        packageId,
                        includePrerelease: false,
                        includeUnlisted: false,
                        new SourceCacheContext(),
                        _logger,
                        CancellationToken.None);
                    
                    var latestVersion = metadata
                        .Where(m => !m.Identity.Version.IsPrerelease)
                        .OrderByDescending(m => m.Identity.Version)
                        .FirstOrDefault();
                    
                    if (latestVersion == null)
                        continue;
                    
                    // Download the package
                    var packageIdentity = latestVersion.Identity;
                    var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                        packageIdentity,
                        new PackageDownloadContext(new SourceCacheContext()),
                        globalPackagesFolder: tempDir,
                        logger: _logger,
                        token: CancellationToken.None);
                    
                    if (downloadResult?.PackageStream == null)
                    {
                        Console.WriteLine($"  Failed to download package {packageId}");
                        continue;
                    }
                    
                    Console.WriteLine($"  Successfully downloaded package {packageId}");
                    
                    // Extract types from the package
                    using var packageReader = new PackageArchiveReader(downloadResult.PackageStream);
                    var types = ExtractTypesFromPackage(packageReader);
                    
                    Console.WriteLine($"  Found {types.Count} types in {packageId}");
                    
                    foreach (var type in types)
                    {
                        allTypes.Add(type);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to process package {packageId}: {ex.Message}");
                    // Continue with next package
                }
            }
        }
        finally
        {
            // Clean up temp directory
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
        
        return allTypes.ToList();
    }
    
    private List<string> ExtractTypesFromPackage(PackageArchiveReader packageReader)
    {
        var types = new List<string>();
        
        try
        {
            // Get lib files that are .NET assemblies
            var libItems = packageReader.GetLibItems();
            Console.WriteLine($"    Found {libItems.Count()} lib item groups");
            
            var targetFramework = NuGetFramework.ParseFolder("net8.0") ?? 
                                NuGetFramework.ParseFolder("netstandard2.1") ?? 
                                NuGetFramework.ParseFolder("netstandard2.0");
            
            var compatibleLibItems = libItems
                .Where(lib => DefaultCompatibilityProvider.Instance.IsCompatible(targetFramework, lib.TargetFramework))
                .OrderByDescending(lib => lib.TargetFramework.Version)
                .FirstOrDefault();
            
            Console.WriteLine($"    Compatible lib items: {(compatibleLibItems != null ? "found" : "none")}");
            
            if (compatibleLibItems != null)
            {
                var dlls = compatibleLibItems.Items.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToList();
                Console.WriteLine($"    Found {dlls.Count} DLL files");
                
                foreach (var file in dlls)
                {
                    Console.WriteLine($"    Processing DLL: {file}");
                    try
                    {
                        using var stream = packageReader.GetStream(file);
                        using var memoryStream = new MemoryStream();
                        stream.CopyTo(memoryStream);
                        memoryStream.Position = 0;
                        
                        var assemblyTypes = ExtractTypesFromAssembly(memoryStream);
                        Console.WriteLine($"      Found {assemblyTypes.Count} types in {file}");
                        types.AddRange(assemblyTypes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"      Failed to process {file}: {ex.Message}");
                    }
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
                
                // Skip compiler-generated types, nested types, and private types
                if (typeDef.Attributes.HasFlag(TypeAttributes.NestedPrivate) ||
                    typeDef.Attributes.HasFlag(TypeAttributes.NotPublic))
                    continue;
                
                var typeName = metadataReader.GetString(typeDef.Name);
                
                // Skip compiler-generated types
                if (typeName.StartsWith("<") || typeName.Contains("__") || typeName.StartsWith("Program"))
                    continue;
                
                // Only include public types
                if (typeDef.Attributes.HasFlag(TypeAttributes.Public) || 
                    typeDef.Attributes.HasFlag(TypeAttributes.NestedPublic))
                {
                    types.Add(typeName);
                }
            }
        }
        catch
        {
            // Skip assemblies that can't be analyzed
        }
        
        return types;
    }
}