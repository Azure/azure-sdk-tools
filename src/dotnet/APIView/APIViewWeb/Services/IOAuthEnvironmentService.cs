namespace APIViewWeb.Services
{
    public interface IOAuthEnvironmentService
    {
        /// <summary>
        /// Gets the canonical host for the current environment (e.g., "apiviewstaging.azurewebsites.net")
        /// </summary>
        string GetCanonicalHost();

        /// <summary>
        /// Gets all vanity domains for the current environment
        /// </summary>
        string[] GetVanityDomains();

        /// <summary>
        /// Checks if the given host is the canonical host for this environment
        /// </summary>
        bool IsCanonicalHost(string host);

        /// <summary>
        /// Checks if the given host is a vanity domain for this environment
        /// </summary>
        bool IsVanityDomain(string host);

        /// <summary>
        /// Gets all allowed hosts (canonical + vanity domains) for this environment
        /// </summary>
        string[] GetAllAllowedHosts();
    }
}
