// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
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
}
