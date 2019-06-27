// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods
{
    using System;
    public static class StringExtensionMethods
    {
        /// <summary>
        /// Does matching based on provided stringComparison option
        /// </summary>
        /// <param name="lhsArg"></param>
        /// <param name="rhsArg"></param>
        /// <param name="comparisonType"></param>
        /// <returns></returns>
        public static bool Contains(this string lhsArg, string rhsArg, StringComparison comparisonType)
        {
            if (string.IsNullOrWhiteSpace(lhsArg) || string.IsNullOrWhiteSpace(rhsArg))
                return false;

            if (string.IsNullOrWhiteSpace(lhsArg) && string.IsNullOrWhiteSpace(rhsArg))
                return true;

            return lhsArg.IndexOf(rhsArg, comparisonType) >= 0;
        }
    }
}
