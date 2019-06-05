// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace MS.Az.Mgmt.CI.Common.ExtensionMethods
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    public static class CollectionExtensionMethods
    {
        /// <summary>
        /// Find if the collection is null or empty
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <returns></returns>
        public static bool NotNullOrAny<T>(this IEnumerable<T> source)
        {
            if (source != null && source.Any<T>())
                return true;
            else
                return false;

            //if (collection?.Any<string>() != true)
            //if (containsTokenFiltered?.Any<string>() != true)
            //if ((doesNotContainsTokenFiltered != null) && (doesNotContainsTokenFiltered.Any<string>()))
        }
    }
}
