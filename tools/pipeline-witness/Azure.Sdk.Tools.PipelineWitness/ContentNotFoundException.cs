using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.PipelineWitness
{

    [Serializable]
    public class ContentNotFoundException : Exception
    {
        public ContentNotFoundException() { }
        public ContentNotFoundException(Uri contentUri) : base($"Content not found at URL: {contentUri}.") { }
        public ContentNotFoundException(Uri contentUri, Exception inner) : base($"Content not found at URL: {contentUri}.", inner) { }
        protected ContentNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
