using System;

namespace Azure.Sdk.Tools.PipelineWitness.Services;

internal class MessageProcessingException : Exception
{
    public MessageProcessingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
