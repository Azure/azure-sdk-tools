using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Text.Json;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class SanitizerTests
    {
        [Fact]
        public void TestSubscriptionIdReplace()
        {
            var session = TestHelpers.LoadRecordSession("Test.RecordEntries/post_delete_get_content.json");
            
            /// request String.Format("https://management.azure.com/subscriptions/{0}?api-version=2020-01-01", System.Guid.NewGuid().ToString()(
        }
    }
}
