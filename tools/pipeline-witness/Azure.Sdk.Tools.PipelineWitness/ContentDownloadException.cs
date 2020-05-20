using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.PipelineWitness
{

    [Serializable]
    public class ContentDownloadException : Exception
    {
        public ContentDownloadException() { }
        public ContentDownloadException(string message) : base(message) { }
        public ContentDownloadException(string message, Exception inner) : base(message, inner) { }
        protected ContentDownloadException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
