// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.Utilities
{
    using System;
    using System.Collections.Generic;

    public static class GlobalStateInfo
    {
        static Dictionary<string, object> GlobalObjectCache;

        static GlobalStateInfo()
        {
            GlobalObjectCache = new Dictionary<string, object>();
        }

        public static T GetGlobalObject<T>() where T: class
        {
            object returnObj = null;
            Type objType = typeof(T);
            string typeName = objType.FullName;
            if(GlobalObjectCache.ContainsKey(typeName))
            {
                returnObj = GlobalObjectCache[typeName];
            }

            return returnObj as T;
        }

        public static void SetGlobalObject<T>(T objToCache) where T: class
        {
            Type objType = typeof(T);
            string typeName = objType.FullName;
            if (!GlobalObjectCache.ContainsKey(typeName))
            {
                GlobalObjectCache.Add(typeName, objToCache);
            }
        }
    }
}
