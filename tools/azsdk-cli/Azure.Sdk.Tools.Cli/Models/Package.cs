namespace Azure.Sdk.Tools.Cli.Models
{
    public class Package
    {
        public int WorkItemId { get; set; }
        public string WorkItemUrl { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PackageType { get; set; } = string.Empty;
        public string PackageRepoPath { get; set; } = string.Empty;        
        public string LatestPipelineRun { get; set; } = string.Empty;
        public string PipelineDefinitionUrl { get; set; } = string.Empty;
        public bool IsChangeLogReady {
            get
            {
                return !changeLogStatus.Equals("Failed", StringComparison.OrdinalIgnoreCase);
            }
        }
        public string changeLogStatus { get; set; } = string.Empty;
        public string ChangeLogValidationDetails { get; set; } = string.Empty;
        public bool IsApiViewApproved
        {
            get
            {
                return APIViewStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase);
            }
        }
        public string APIViewStatus { get; set; } = string.Empty;
        public string ApiViewValidationDetails { get; set; } = string.Empty;
        public bool IsPackageNameApproved {
            get
            {
                return PackageNameStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase);
            }
        }
        public string PackageNameStatus { get; set; } = string.Empty;
        public string PackageNameApprovalDetails { get; set; } = string.Empty;
        public List<SDKReleaseInfo> PlannedReleases = [];
        public List<SDKReleaseInfo> ReleasedVersions = [];
    }

    public class SDKReleaseInfo
    {
        public string Version { get; set; }
        public string ReleaseDate { get; set; }
        public string ReleaseType { get; set; }
    }
}
