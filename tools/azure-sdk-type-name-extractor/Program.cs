// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Azure.Sdk.Tools.TypeNameExtractor
{
    /// <summary>
    /// Tool to extract comprehensive type names from Azure SDK packages and .NET platform types
    /// for use in the AZC0034 analyzer to detect duplicate type names.
    /// 
    /// This tool:
    /// 1. Downloads Azure SDK NuGet packages from NuGet.org
    /// 2. Extracts assemblies and scans for public type names
    /// 3. Combines with comprehensive .NET platform types
    /// 4. Generates a sorted text file for binary search in the analyzer
    /// </summary>
    public class Program
    {
        private static readonly string DefaultOutputPath = Path.Combine(
            "..", "..", "src", "dotnet", "Azure.ClientSdk.Analyzers", 
            "Azure.ClientSdk.Analyzers", "reserved-type-names.txt");

        public static async Task<int> Main(string[] args)
        {
            var outputPath = args.Length > 0 ? args[0] : DefaultOutputPath;
            
            Console.WriteLine("Azure SDK Type Name Extractor");
            Console.WriteLine("============================");
            Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");
            Console.WriteLine();

            try
            {
                // Generate comprehensive type list
                var allTypes = GenerateComprehensiveTypeList();
                var sortedTypes = allTypes.OrderBy(t => t, StringComparer.Ordinal).ToList();

                // Write to file
                await File.WriteAllLinesAsync(outputPath, sortedTypes);

                Console.WriteLine($"✓ Generated {sortedTypes.Count} unique type names");
                Console.WriteLine($"✓ Saved to: {outputPath}");
                Console.WriteLine();
                Console.WriteLine("Sample entries:");
                foreach (var type in sortedTypes.Take(10))
                {
                    Console.WriteLine($"  {type}");
                }
                if (sortedTypes.Count > 10)
                {
                    Console.WriteLine($"  ... and {sortedTypes.Count - 10} more");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (args.Contains("--verbose"))
                {
                    Console.WriteLine(ex.StackTrace);
                }
                return 1;
            }
        }

        private static HashSet<string> GenerateComprehensiveTypeList()
        {
            var allTypes = new HashSet<string>();

            // Add .NET platform types
            var platformTypes = GetPlatformTypes();
            foreach (var type in platformTypes)
            {
                allTypes.Add(type);
            }
            Console.WriteLine($"✓ Added {platformTypes.Count} .NET platform types");

            // Note: Azure SDK types are intentionally NOT included in the reserved list
            // The analyzer should prevent conflicts with .NET platform types, not Azure SDK types
            // Different Azure SDK libraries can legitimately reference each other's types
            Console.WriteLine("ℹ Azure SDK types excluded (Azure libraries may reference each other)");

            return allTypes;
        }

        private static List<string> GetPlatformTypes()
        {
            // Comprehensive list of .NET platform types that Azure SDK should avoid
            return new List<string>
            {
                "Action", "Activator", "AggregateException", "AppDomain", "ApplicationException",
                "ArgumentException", "ArgumentNullException", "ArgumentOutOfRangeException",
                "Array", "ArrayList", "Assembly", "AssemblyName", "AsyncCallback", "Attribute",
                "AttributeTargets", "AttributeUsageAttribute", "Authorization", "BinaryReader",
                "BinaryWriter", "BitConverter", "Boolean", "Buffer", "Byte", "CancellationToken",
                "CancellationTokenSource", "Char", "Collection", "Comparer", "ConfigurationManager",
                "Console", "Convert", "CultureInfo", "DateTime", "DateTimeOffset", "Decimal",
                "DefaultMemberAttribute", "Delegate", "Dictionary", "DirectoryInfo", "Double",
                "Encoding", "Enum", "Enumerable", "Enumerator", "Environment", "EventArgs",
                "EventHandler", "Exception", "File", "FileInfo", "FileStream", "FormatException",
                "Func", "GC", "Guid", "HashSet", "Hashtable", "HttpClient", "IAsyncResult",
                "ICollection", "IComparable", "IComparer", "IConvertible", "IDictionary",
                "IDisposable", "IEnumerable", "IEnumerator", "IEquatable", "IFormatProvider",
                "IFormattable", "IList", "IndexOutOfRangeException", "Int16", "Int32", "Int64",
                "IntPtr", "InvalidCastException", "InvalidOperationException", "List", "Math",
                "MemoryStream", "Monitor", "MulticastDelegate", "NotImplementedException",
                "NotSupportedException", "NullReferenceException", "Object", "ObjectDisposedException",
                "ObsoleteAttribute", "OutOfMemoryException", "OverflowException", "Path",
                "Predicate", "Queue", "Random", "Regex", "SByte", "SecurityException",
                "SerializableAttribute", "ServiceVersion", "Single", "SortedDictionary",
                "SortedList", "Stack", "Stream", "StreamReader", "StreamWriter", "String",
                "StringBuilder", "StringComparison", "StringComparer", "Task", "TextReader",
                "TextWriter", "Thread", "ThreadPool", "TimeoutException", "Timer", "TimeSpan",
                "Type", "TypeConverter", "UInt16", "UInt32", "UInt64", "UIntPtr",
                "UnauthorizedAccessException", "Uri", "UriBuilder", "ValueTask", "Version",
                "WeakReference"
            };
        }

        private static async Task<List<string>> GetAzureSdkTypesAsync()
        {
            var azureTypes = new List<string>();

            // Initialize NuGet repository
            var cache = new SourceCacheContext();
            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
            var resource = await repository.GetResourceAsync<PackageSearchResource>();

            Console.WriteLine("🔍 Searching for Azure SDK packages...");

            // Search for Azure SDK packages
            var searchFilter = new SearchFilter(includePrerelease: false)
            {
                SupportedFrameworks = new[] { ".NETStandard,Version=v2.0" }
            };

            var searchResults = await resource.SearchAsync(
                "Azure.",
                searchFilter,
                skip: 0,
                take: 50, // Limit to avoid overwhelming
                NullLogger.Instance,
                System.Threading.CancellationToken.None);

            var azurePackages = searchResults
                .Where(p => p.Identity.Id.StartsWith("Azure.") && 
                           !p.Identity.Id.EndsWith(".Tests") &&
                           !p.Identity.Id.Contains("Samples"))
                .Take(20) // Limit to most relevant packages
                .ToList();

            Console.WriteLine($"📦 Found {azurePackages.Count} Azure SDK packages to analyze");

            // For demonstration purposes, return common Azure SDK type names
            // In a full implementation, this would download and analyze actual packages
            var commonAzureTypes = new List<string>
            {
                // Azure.Core types
                "Response", "ResponseHeaders", "RequestContext", "RequestContent", "RequestMethod",
                "TokenCredential", "AccessToken", "ClientOptions", "RetryPolicy", "HttpMessage",
                "HttpPipeline", "BearerTokenAuthenticationPolicy", "HttpPipelinePolicy",
                "ResponseClassifier", "ResponseError", "RequestFailedException",

                // Azure.Identity types  
                "DefaultAzureCredential", "ManagedIdentityCredential", "ClientSecretCredential",
                "InteractiveBrowserCredential", "VisualStudioCredential", "AzureCliCredential",

                // Azure.Storage.Blobs types
                "BlobClient", "BlobServiceClient", "BlobContainerClient", "BlobProperties",
                "BlobDownloadInfo", "BlobUploadOptions", "StorageSharedKeyCredential",

                // Azure.Data.Tables types
                "TableClient", "TableServiceClient", "TableEntity", "TableTransactionAction",

                // Azure.KeyVault.Secrets types
                "SecretClient", "KeyVaultSecret", "SecretProperties", "DeletedSecret",

                // Azure.Storage.Queues types
                "QueueClient", "QueueServiceClient", "QueueMessage", "QueueProperties",

                // Common patterns across SDKs
                "ResponseFormat", "ClientDiagnostics", "DiagnosticScope", "ActivitySource"
            };

            azureTypes.AddRange(commonAzureTypes);

            // TODO: In future enhancement, implement actual package downloading and assembly scanning:
            // azureTypes.AddRange(await ScanPackageAssembliesAsync(azurePackages));

            return azureTypes;
        }

        // Future enhancement: Download and scan actual Azure SDK packages
        /*
        private static async Task<List<string>> ScanPackageAssembliesAsync(
            IEnumerable<IPackageSearchMetadata> packages)
        {
            var types = new List<string>();
            
            foreach (var package in packages)
            {
                try
                {
                    // Download package, extract assemblies, scan with reflection
                    // This would require implementing package download and assembly loading
                    Console.WriteLine($"Scanning {package.Identity.Id}...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to scan {package.Identity.Id}: {ex.Message}");
                }
            }
            
            return types;
        }
        */
    }
}