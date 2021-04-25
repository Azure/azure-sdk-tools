using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Transforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy
{
    public static class SessionConfiguration
    {
        private static RecordedTestSanitizer _sanitizer = new RecordedTestSanitizer();

        public static List<RecordedTestSanitizer> Sanitizers = new List<RecordedTestSanitizer>
        {
            // by default, include standadard santizier
            _sanitizer
        };

        public static List<ResponseTransform> Transforms = new List<ResponseTransform>
        {
            new StorageRequestIdTransform()
        };
    }
}
