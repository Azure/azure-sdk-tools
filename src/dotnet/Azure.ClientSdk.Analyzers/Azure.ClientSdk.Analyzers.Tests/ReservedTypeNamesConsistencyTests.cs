// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace Azure.ClientSdk.Analyzers.Tests
{
    public class ReservedTypeNamesConsistencyTests
    {
        [Fact]
        public void ReservedTypeNamesAndQualifiedNamesAreConsistent()
        {
            // This test ensures that the reserved-type-names.txt and reserved-type-qualified-names.txt 
            // files are consistent with each other, i.e., the type names extracted from the qualified 
            // names match the reserved type names at the same indices.
            
            var reservedNames = LoadReservedTypeNames();
            var qualifiedNames = LoadQualifiedTypeNames();
            
            Assert.Equal(reservedNames.Length, qualifiedNames.Length);
            
            for (int i = 0; i < reservedNames.Length; i++)
            {
                var qualifiedEntry = qualifiedNames[i];
                var semicolonIndex = qualifiedEntry.IndexOf(';');
                var qualifiedTypeName = semicolonIndex >= 0 ? qualifiedEntry.Substring(0, semicolonIndex) : qualifiedEntry;
                
                var lastDotIndex = qualifiedTypeName.LastIndexOf('.');
                var extractedTypeName = lastDotIndex >= 0 ? qualifiedTypeName.Substring(lastDotIndex + 1) : qualifiedTypeName;
                
                Assert.True(
                    string.Equals(reservedNames[i], extractedTypeName, StringComparison.Ordinal),
                    $"Mismatch at index {i}: expected '{reservedNames[i]}' but qualified name contains '{extractedTypeName}' (from '{qualifiedTypeName}')");
            }
        }

        [Fact]
        public void ReservedTypeNamesAreSorted()
        {
            // This test ensures that the reserved-type-names.txt file is properly sorted
            // as expected by the DuplicateTypeNameAnalyzer.
            
            var reservedNames = LoadReservedTypeNames();
            
            for (int i = 1; i < reservedNames.Length; i++)
            {
                var comparison = StringComparer.Ordinal.Compare(reservedNames[i - 1], reservedNames[i]);
                Assert.True(comparison < 0, 
                    $"Reserved type names file is not sorted. '{reservedNames[i - 1]}' comes before '{reservedNames[i]}' at index {i}");
            }
        }

        private static string[] LoadReservedTypeNames()
        {
            var assembly = typeof(DuplicateTypeNameAnalyzer).GetTypeInfo().Assembly;
            var resourceName = "Azure.ClientSdk.Analyzers.reserved-type-names.txt";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Could not find embedded resource: " + resourceName);
                }
                
                using (var reader = new StreamReader(stream))
                {
                    var names = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            names.Add(line);
                        }
                    }
                    return names.ToArray();
                }
            }
        }

        private static string[] LoadQualifiedTypeNames()
        {
            var assembly = typeof(DuplicateTypeNameAnalyzer).GetTypeInfo().Assembly;
            var resourceName = "Azure.ClientSdk.Analyzers.reserved-type-qualified-names.txt";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Could not find embedded resource: " + resourceName);
                }
                
                using (var reader = new StreamReader(stream))
                {
                    var names = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            names.Add(line);
                        }
                    }
                    return names.ToArray();
                }
            }
        }
    }
}