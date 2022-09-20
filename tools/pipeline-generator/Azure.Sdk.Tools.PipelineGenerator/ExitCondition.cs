using System;

namespace PipelineGenerator
{
    public enum ExitCondition
    {
        Success = 0,
        Exception = 1,
        InvalidArguments = 2,
        NoComponentsFound = 3,
        DuplicateComponentsFound = 4
    }
}