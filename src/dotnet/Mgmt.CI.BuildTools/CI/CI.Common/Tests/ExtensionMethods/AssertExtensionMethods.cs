// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace Tests.CI.Common.ExtensionMethods
{
    using Tests.CI.Common.Base;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xunit;

    static public class AssertExtensionMethods
    {
        static public void DoesFileExists(this Assert assert, bool expectToExist, Func<bool> actualExistDelegate, string assertFailureInfoMessage = "")
        {
            bool delegateValue = actualExistDelegate();

            if (expectToExist != delegateValue)
            {
                GlobalTestInfo.GlobalTestOutput.WriteLine(assertFailureInfoMessage);
            }

            Assert.Equal(expectToExist, delegateValue);
        }
    }
}
