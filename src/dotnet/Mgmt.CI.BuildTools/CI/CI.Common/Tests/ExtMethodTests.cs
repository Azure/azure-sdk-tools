// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.Common
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using Xunit;

    public class EnumExtMethodTests
    {
        [Fact]
        public void FindAttributeForField()
        {
            string sunDayDescription = ExtMethodEnumAsset.Sunday.GetAttributeInfoForEnum<string, DescriptionAttribute>((attrib) => attrib.Description);
            Assert.Contains("lazy", sunDayDescription, StringComparison.OrdinalIgnoreCase);

            string altDescription = ExtMethodEnumAsset.Monday.GetDescriptionAttributeValue();
            Assert.Contains("opPosite", altDescription, StringComparison.OrdinalIgnoreCase);
        }
    }


    public class ExtMethodTestAssetClass
    {
        [Description("Random Description On Field")]
        public string SomeClassField;

        public ExtMethodEnumAsset WeekDay;
    }

    public enum ExtMethodEnumAsset
    {
        [Description("Lazy Day")]
        Sunday,
        [Description("Opposite to Lazy Day")]
        Monday
    }
}
