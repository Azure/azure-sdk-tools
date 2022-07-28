namespace Azure.Sdk.Tools.PipelineOwnersExtractor.Configuration
{
    public class PipelineOwnerSettings
    {
        public string Account { get; set; }

        public string Projects { get; set; }

        public string OpenSourceAadAppId { get; set; }

        public string OpenSourceAadSecret { get; set; }

        public string OpenSourceAadTenantId { get; set; }

        public string AzureDevOpsPat { get; set; }

        public string Output { get; set; }
    }
}
