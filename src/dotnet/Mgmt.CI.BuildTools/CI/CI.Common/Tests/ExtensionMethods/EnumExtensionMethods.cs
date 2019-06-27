// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace Tests.CI.Common.ExtensionMethods
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Reflection;
    using System.Text;
    using Tests.CI.Common.Base;

    static public class EnumExtensionMethods
    {
        private static T GetAttributeForField<T>(Enum enumMember) where T : Attribute
        {
            MemberInfo mi = enumMember.GetType().GetField(enumMember.ToString());
            T attrib = Attribute.GetCustomAttribute(mi, typeof(T)) as T;
            return attrib;
        }
    }
}
