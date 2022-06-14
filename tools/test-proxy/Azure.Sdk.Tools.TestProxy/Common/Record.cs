using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RecordingHandlerSession
    {
        public RecordingHandlerSession() { }

        public string Path { get; set; }

        public ModifiableRecordSession Recording { get; set; }

        public HttpClient Client { get; set; }
    }
}
