// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Tools.Package;

namespace Azure.Sdk.Tools.Cli.Models.ClassifyItems
{
    /// <summary>
    /// Represents the response from a classification operation
    /// </summary>
    public abstract class ClassifyResponse
    {
        /// <summary>The type of classification that was performed.</summary>
        public ClassificationKind ClassifyType { get;}

        protected ClassifyResponse(ClassificationKind classifyType)
        {
            ClassifyType = classifyType;
        }
    }
    public class ClassifyResponse<T> : ClassifyResponse
    {
        /// <summary>The classification result. The concrete type depends on <see cref="ClassifyType"/>.</summary>
        public List<T>? ClassifiedResult { get; set; }
        protected ClassifyResponse(ClassificationKind classifyType, List<T>? classifiedResult): base(classifyType)
        {
            ClassifiedResult = classifiedResult;
        }
    }

    public sealed class ClassifySdkBreakingChangesResponse : ClassifyResponse<SdkBreakingChange>
    {
        public ClassifySdkBreakingChangesResponse(List<SdkBreakingChange>? classifiedResult)
            : base(ClassificationKind.SdkBreakingChange, classifiedResult)
        {
        }
    }

    public sealed class ClassifyCustomizationResponse : ClassifyResponse<FeedbackItemClassificationDetails>
    {
        public ClassifyCustomizationResponse(List<FeedbackItemClassificationDetails>? classifiedResult)
            : base(ClassificationKind.Customization, classifiedResult)
        {
        }
    }
}
