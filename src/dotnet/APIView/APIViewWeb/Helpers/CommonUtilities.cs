// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;

namespace APIViewWeb.Helpers
{

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
