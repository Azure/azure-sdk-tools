// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Models.ClassifyItems
{
    /// <summary>
    /// Represents the response from a classification operation
    /// </summary>
    public class ClassifyResponse
    {
        /// <summary>The type of classification that was performed.</summary>
        public ClassificationKind ClassifyType { get; set; }

        /// <summary>The classification result. The concrete type depends on <see cref="ClassifyType"/>.</summary>
        public object? ClassifiedResult { get; set; }
        public ClassifyResponse(ClassificationKind classifyType, object? classifiedResult)
        {
            ClassifyType = classifyType;
            ClassifiedResult = classifiedResult;
        }
    }
}
