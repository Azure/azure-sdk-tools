namespace Azure.Sdk.Tools.NotificationConfiguration.Models
{
    /// <summary>
    /// Object describing employee github mapping information.
    /// </summary>
    /// <remarks>
    /// This class is intended for easy transport of employee github link details at build time.
    /// </remarks>
    public class IdentityDetail
    {
        /// <summary>
        /// Github User Name. Mapped from githubemployeelink column githubUserName
        /// </summary>
        public string GithubUserName { get; set; }

        /// <summary>
        /// Azure Active Directory Id. Mapped from githubemployeelink column aadId
        /// </summary>
        public string AadId { get; set; }

        /// <summary>
        /// Full Name of Employee. Mapped from githubemployeelink column aadName
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// MS Alias of employee. Mapped from githubemployeelink column aadAlias
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Full email address. Mapped from githubemployeelink column aadUpn
        /// </summary>
        public string AadUpn { get; set; }
    }
}
