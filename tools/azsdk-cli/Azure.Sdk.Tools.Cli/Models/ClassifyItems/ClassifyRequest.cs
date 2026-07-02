namespace Azure.Sdk.Tools.Cli.Models.ClassifyItems
{
    public class ClassifyRequest
    {
        public ClassifyType ClassifyType { get; set; }
        public string Language { get; set; }
        public string? TspProjectPath { get; set; }

        public bool IsBatchClassify { get; set; } = false;

        public int BatchSize { get; set; } = 50;
        public ClassifyRequest(ClassifyType classifyType, string language, string? tspProjectPath)
        {
            ClassifyType = classifyType;
            Language = language;
            TspProjectPath = tspProjectPath;
        }
    }

    public class ClassifySdkBreakingChangesRequest : ClassifyRequest
    {
        public string SdkChange { get; set; }
        public string SdkBreakingPattern { get; set; }
        public ClassifySdkBreakingChangesRequest(string sdkChange, string sdkRepoRoot, string sdkBreakingPattern, string language, string? tspProjectPath) : base(ClassifyType.SdkBreakingChange, language, tspProjectPath)
        {
            SdkChange = sdkChange;
            SdkBreakingPattern = sdkBreakingPattern;
        }
    }

    public class ClassifyCustomizationRequest : ClassifyRequest
    {
        public string? ServiceName { get; set; }
        public string ReferencePatternContent { get; set; }

        public string GlobalContext { get; set; } = string.Empty;

        public EditScope EditScope { get; set; }

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
