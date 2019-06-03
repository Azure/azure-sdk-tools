// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.Common.Base
{
    using System;
    using System.Collections.Generic;
    using System.Text;


    public class SharedXUnitTestFixture : IDisposable
    {
        public virtual void Dispose()
        {
            //Add common cleanup
        }
    }
}
