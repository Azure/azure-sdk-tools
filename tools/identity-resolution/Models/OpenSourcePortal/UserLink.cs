namespace Models.OpenSourcePortal
{
    public class UserLink
    {
        public GitHubUserDetail GitHub { get; set; }

        public AadUserDetail Aad { get; set; }

        public bool IsServiceAccount { get; set; }

        public string ServiceAccountContact { get; set; }
    }
}
