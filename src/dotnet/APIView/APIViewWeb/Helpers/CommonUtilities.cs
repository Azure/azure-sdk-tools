// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace APIViewWeb.Helpers
{
    /// <summary>
    /// General utility class for date/time operations and common helper methods
    /// </summary>
    public static class DateTimeHelper
    {
        /*
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
        */
    }

    /// <summary>
    /// String manipulation and validation utilities
    /// </summary>
    public static class StringHelper
    {
        /// <summary>
        /// Extract service name from package name across different language formats
        /// </summary>
        /// <param name="packageName">The package name to extract from</param>
        /// <returns>The extracted service name or "Unknown" if extraction fails</returns>
        public static string ExtractServiceName(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return "Unknown";

            try
            {
                // .NET format: Microsoft.Azure.ServiceName or Microsoft.ServiceName
                if (packageName.StartsWith("Microsoft."))
                {
                    var parts = packageName.Split('.');
                    if (parts.Length >= 3 && parts[1].Equals("Azure", StringComparison.OrdinalIgnoreCase))
                    {
                        return parts[2]; // Microsoft.Azure.ServiceName
                    }
                    else if (parts.Length >= 2)
                    {
                        return parts[1]; // Microsoft.ServiceName
                    }
                }

                // Java format: com.azure.resourcemanager:azure-resourcemanager-servicename
                if (packageName.Contains("azure-resourcemanager-"))
                {
                    var match = Regex.Match(packageName, @"azure-resourcemanager-(.+)");
                    if (match.Success)
                    {
                        return CapitalizeServiceName(match.Groups[1].Value);
                    }
                }

                // Python format: azure-mgmt-servicename
                if (packageName.StartsWith("azure-mgmt-"))
                {
                    var serviceName = packageName.Substring("azure-mgmt-".Length);
                    return CapitalizeServiceName(serviceName);
                }

                // JavaScript format: @azure/arm-servicename
                if (packageName.StartsWith("@azure/arm-"))
                {
                    var serviceName = packageName.Substring("@azure/arm-".Length);
                    return CapitalizeServiceName(serviceName);
                }

                // Generic extraction for other patterns
                var genericMatch = Regex.Match(packageName, @"(?:azure[.-]?)?(?:mgmt[.-]?)?(?:arm[.-]?)?(?:resourcemanager[.-]?)?(.+)", RegexOptions.IgnoreCase);
                if (genericMatch.Success && !string.IsNullOrWhiteSpace(genericMatch.Groups[1].Value))
                {
                    return CapitalizeServiceName(genericMatch.Groups[1].Value);
                }

                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Capitalize and clean up service name
        /// </summary>
        /// <param name="serviceName">The service name to process</param>
        /// <returns>The processed service name</returns>
        private static string CapitalizeServiceName(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                return "Unknown";

            // Remove common suffixes and prefixes
            serviceName = serviceName.Replace("-", "").Replace("_", "").Replace(".", "");
            
            // Capitalize first letter
            return char.ToUpper(serviceName[0]) + serviceName.Substring(1).ToLower();
        }
    }

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
            var sdkLanguages = new[] { "C#", "Java", "Python", "Go", "JavaScript" };
            return sdkLanguages.Contains(language);
        }

        /// <summary>
        /// Check if the language is an SDK language or TypeSpec
        /// </summary>
        /// <param name="language">The language to check</param>
        /// <returns>True if the language is an SDK language or TypeSpec</returns>
        public static bool IsSDKLanguageOrTypeSpec(string language)
        {
            var supportedLanguages = new[] { "TypeSpec", "C#", "Java", "Python", "Go", "JavaScript" };
            return supportedLanguages.Contains(language);
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
            => DateTimeHelper.CalculateBusinessDays(startDate, businessDays);
        }
    }
    */
}
