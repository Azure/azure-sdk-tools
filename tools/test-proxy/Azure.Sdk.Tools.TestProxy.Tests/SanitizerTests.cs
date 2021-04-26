using System;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class SanitizerTests
    {
        [Fact]
        public void TestSubscriptionIdReplace()
        {
            /// request String.Format("https://management.azure.com/subscriptions/{0}?api-version=2020-01-01", System.Guid.NewGuid().ToString()(
        }
    }
}
