using System;

namespace Microsoft.Azure.Sdk.PipelineGenerator.Tool
{
    public enum ExitCondition
    {
        Success = 0,
        Exception = 1,
        InvalidArguments = 2,
        NoComponentsFound = 3
    }
}