namespace Models.OpenSourcePortal
{
    public class GitHubUserDetail
    {
        public int Id { get; set; }

        public string Login { get; set; }

        public string[] Organizations { get; set; }

        public string Avatar { get; set; }
    }
}
