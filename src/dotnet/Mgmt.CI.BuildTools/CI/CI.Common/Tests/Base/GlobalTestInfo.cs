// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.Common
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xunit.Abstractions;

    static public class GlobalTestInfo
    {
        #region Fields
        //static ITestOutputHelper _globalTestoutput;
        #endregion
        static public ITestOutputHelper GlobalTestOutput { get; set; }
    }
}
