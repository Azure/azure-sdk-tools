// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Tests.CI.Common.Services
{
    using MS.Az.Mgmt.CI.BuildTasks.Common;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Services;
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Tests.CI.Common.Base;
    using Xunit;
    public class KVClientTests : BuildTasksTestBase
    {
        [Fact(Skip ="setup with new KV")]
        public void GetCertFromKV()
        {
            KeyVaultService kvClient = new KeyVaultService();
            string accToken = kvClient.GetSecret("");            
            Assert.Equal("adxsdknet", accToken);
        }
    }
}
