namespace Azure.Sdk.Tools.Cli.Models.ClassifyItems
{
    /// <summary>
    /// Base class for classification requests.
    /// </summary>
    public class ClassifyRequest
    {
        /// <summary>The type of classification to perform.</summary>
        public ClassifyType ClassifyType { get; set; }

        /// <summary>The target SDK language (e.g., "dotnet", "java", "python").</summary>
        public string Language { get; set; }

        /// <summary>Optional path to the TypeSpec project directory.</summary>
        public string? TspProjectPath { get; set; }

        /// <summary>Indicates whether the classification should be performed in batch mode.</summary>
        public bool IsBatchClassify { get; set; } = false;

        /// <summary>The number of items to classify per batch when <see cref="IsBatchClassify"/> is enabled.</summary>
        public int BatchSize { get; set; } = 50;
        public ClassifyRequest(ClassifyType classifyType, string language, string? tspProjectPath)
        {
            ClassifyType = classifyType;
            Language = language;
            TspProjectPath = tspProjectPath;
        }
    }

    /// <summary>
    /// Request to classify SDK breaking changes.
    /// </summary>
    public class ClassifySdkBreakingChangesRequest : ClassifyRequest
    {
        /// <summary>The SDK change content to evaluate for breaking changes.</summary>
        public string SdkChange { get; set; }

        /// <summary>The pattern or rules used to determine whether the SDK change is breaking.</summary>
        public string SdkBreakingPattern { get; set; }
        public ClassifySdkBreakingChangesRequest(string sdkChange, string sdkRepoRoot, string sdkBreakingPattern, string language, string? tspProjectPath) : base(ClassifyType.SdkBreakingChange, language, tspProjectPath)
        {
            SdkChange = sdkChange;
            SdkBreakingPattern = sdkBreakingPattern;
        }
    }

    /// <summary>
    /// Request to classify customization items such as feedback.
    /// </summary>
    public class ClassifyCustomizationRequest : ClassifyRequest
    {
        /// <summary>Optional name of the Azure service associated with the customization request.</summary>
        public string? ServiceName { get; set; }

        /// <summary>Reference pattern content used as guidance for classifying customization items.</summary>
        public string ReferencePatternContent { get; set; }

        /// <summary>Additional global context provided to the classifier for all items in this request.</summary>
        public string GlobalContext { get; set; } = string.Empty;

        /// <summary>The edit scope that determines which types of changes are permitted (e.g., spec inputs, custom code, all).</summary>
        public EditScope EditScope { get; set; }

        /// <summary>The list of feedback items to classify.</summary>
        public List<FeedbackItem> Items { get; set; }
        public ClassifyCustomizationRequest(string? serviceName, string referencePatternContent, List<FeedbackItem> items, string language, string? tspProjectPath) : base(ClassifyType.Customization, language, tspProjectPath)
        {
            ServiceName = serviceName;
            ReferencePatternContent = referencePatternContent;
            Items = items;
            IsBatchClassify = true; // Customization classification is always batch classify
        }
    }
}
