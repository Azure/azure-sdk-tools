// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace APIViewWeb.Helpers
{
    /*
    /// <summary>
    /// General utility class for date/time operations and common helper methods
    /// TODO: Auto-approval feature is currently disabled - commenting out for future use
    /// </summary>
    public static class DateTimeHelper
    {
        /// <summary>
        /// Calculate business days from a start date, excluding weekends
        /// TODO: Auto-approval feature is currently disabled - commenting out for future use
        /// </summary>
        /// <param name="startDate">The starting date</param>
        /// <param name="businessDays">Number of business days to add</param>
        /// <returns>The calculated date after adding the specified business days</returns>
        public static DateTime CalculateBusinessDays(DateTime startDate, int businessDays)
        {
            var currentDate = startDate;
            var daysAdded = 0;

            while (daysAdded < businessDays)
            {
                currentDate = currentDate.AddDays(1);
                if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
                {
                    daysAdded++;
                }
            }

            return currentDate;
        }
    }
    */




    /// <summary>
    /// Language detection and validation utilities for APIView
    /// </summary>
    public static class LanguageHelper
    {
        /// <summary>
        /// Check if the language is an SDK language (excludes TypeSpec)
        /// </summary>
        /// <param name="language">The language to check</param>
        /// <returns>True if the language is an SDK language</returns>
        public static bool IsSDKLanguage(string language)
        {
            return ApiViewConstants.SdkLanguages.Contains(language);
        }

        /// <summary>
        /// Check if the language is an SDK language or TypeSpec
        /// </summary>
        /// <param name="language">The language to check</param>
        /// <returns>True if the language is an SDK language or TypeSpec</returns>
        public static bool IsSDKLanguageOrTypeSpec(string language)
        {
            return ApiViewConstants.AllSupportedLanguages.Contains(language);
        }
    }

    /// <summary>
    /// Represents the plane classification of a package
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PackageType
    {
        /// <summary>
        /// Data plane package (client libraries for Azure services)
        /// </summary>
        Data,
        
        /// <summary>
        /// Management plane package (resource management libraries)
        /// </summary>
        Management,
        
        /// <summary>
        /// Cannot determine plane type from package name
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Package classification utilities for distinguishing data plane vs management plane packages
    /// </summary>
    public static class PackageHelper
    {
        /// <summary>
        /// Classifies a package as Data, Management, or Unknown plane based on package name and language
        /// </summary>
        /// <param name="packageName">The package name to analyze</param>
        /// <param name="language">The programming language (C#, Go, Python, Java, JavaScript)</param>
        /// <returns>PackageType enum indicating Data, Management, or Unknown</returns>
        public static PackageType ClassifyPackageType(string packageName, string language)
        {
            // Validate inputs - return Unknown
            if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(language) || !LanguageHelper.IsSDKLanguage(language))
            {
                return PackageType.Unknown;
            }

            // Normalize language name for comparison
            var normalizedLanguage = language.Trim().ToLowerInvariant();
            
            switch (normalizedLanguage)
            {
                case "c#":
                    // Management: strings that match regex /^Azure\.ResourceManager\./
                    if (packageName.StartsWith("Azure.ResourceManager.", StringComparison.Ordinal))
                        return PackageType.Management;
                    
                    // Data: strings that start with "Azure." (but not management plane)
                    if (packageName.StartsWith("Azure.", StringComparison.Ordinal))
                        return PackageType.Data;
                    
                    // Unknown: packages that don't start with "Azure."
                    return PackageType.Unknown;

                case "python":
                    // Management: strings that match regex /^azure-mgmt(-[a-z]+){1,2}$/
                    if (Regex.IsMatch(packageName, @"^azure-mgmt(-[a-z]+){1,2}$", RegexOptions.None))
                        return PackageType.Management;
                    
                    // Data: strings that match regex /^azure(-[a-z]+){1,3}$/
                    if (Regex.IsMatch(packageName, @"^azure(-[a-z]+){1,3}$", RegexOptions.None))
                        return PackageType.Data;
                    
                    // Unknown: packages that don't match either pattern
                    return PackageType.Unknown;

                case "java":
                    // Management: strings that match regex /^azure-resourcemanager-[^\/]+$/
                    if (Regex.IsMatch(packageName, @"^azure-resourcemanager-[^\/]+$", RegexOptions.None))
                        return PackageType.Management;
                    
                    // Data: strings that match regex /^azure(-\w+)+$/
                    if (Regex.IsMatch(packageName, @"^azure(-\w+)+$", RegexOptions.None))
                        return PackageType.Data;
                    
                    // Unknown: packages that don't match either pattern
                    return PackageType.Unknown;

                case "javascript":
                    // Management: package name pattern /^\@azure\/arm(?:-[a-z]+)+$/ or package-dir pattern /^arm-[^\/]+$/
                    if (Regex.IsMatch(packageName, @"^\@azure\/arm(?:-[a-z]+)+$", RegexOptions.None) ||
                        Regex.IsMatch(packageName, @"^arm-[^\/]+$", RegexOptions.None))
                        return PackageType.Management;
                    
                    // Data: package name pattern /^\@azure-rest\/[a-z]+(?:-[a-z]+)*$/ or package-dir pattern /^(?:[a-z]+-)+rest$/
                    if (Regex.IsMatch(packageName, @"^\@azure-rest\/[a-z]+(?:-[a-z]+)*$", RegexOptions.None) ||
                        Regex.IsMatch(packageName, @"^(?:[a-z]+-)+rest$", RegexOptions.None))
                        return PackageType.Data;
                    
                    // Unknown: packages that don't match either pattern
                    return PackageType.Unknown;

                case "go":
                    // Go packages follow TypeSpec validation rules
                    // Management: contains /resourcemanager/ or /arm prefix patterns
                    if (packageName.Contains("/resourcemanager/", StringComparison.OrdinalIgnoreCase) ||
                        Regex.IsMatch(packageName, @"/arm[^/]*$", RegexOptions.IgnoreCase))
                    {
                        return PackageType.Management;
                    }
                    // Data plane:
                    // service-dir pattern: starts with sdk/ or has /sdk/ in path
                    // emitter-output-dir pattern: contains /az in path
                    else if (Regex.IsMatch(packageName, @"(^|.*/)sdk/", RegexOptions.IgnoreCase) ||
                            Regex.IsMatch(packageName, @"/az[^/]*(/|$)", RegexOptions.IgnoreCase))
                    {
                        return PackageType.Data;
                    }
                    // Unknown: packages that don't match expected Go patterns
                    return PackageType.Unknown;

                default:
                    return PackageType.Unknown;
            }
        }
    }

    /*
    /// <summary>
    /// Backward compatibility alias for existing code
    /// TODO: Auto-approval feature is currently disabled - commenting out for future use
    /// </summary>
    [Obsolete("Use DateTimeHelper instead for better organization")]
    public static class BusinessDayCalculator
    {
        /// <summary>
        /// Calculate business days from a start date, excluding weekends
        /// TODO: Auto-approval feature is currently disabled - commenting out for future use
        /// </summary>
        /// <param name="startDate">The starting date</param>
        /// <param name="businessDays">Number of business days to add</param>
        /// <returns>The calculated date after adding the specified business days</returns>
        public static DateTime CalculateBusinessDays(DateTime startDate, int businessDays)
        {
            return DateTimeHelper.CalculateBusinessDays(startDate, businessDays);
        }
    }
    */
}
