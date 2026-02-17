using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.Services
{
    public class OAuthEnvironmentService : IOAuthEnvironmentService
    {
        private readonly string _canonicalHost;
        private readonly string[] _vanityDomains;

        public OAuthEnvironmentService(IConfiguration configuration)
        {
            // Extract environment key from APPCONFIG_URL
            // e.g., "https://apiviewstaging.azconfig.io" -> "apiviewstaging"
            var appConfigUrl = Environment.GetEnvironmentVariable("APPCONFIG_URL");
            string envKey;

            if (string.IsNullOrEmpty(appConfigUrl))
            {
                // Local development fallback
                envKey = "localhost";
            }
            else
            {
                var uri = new Uri(appConfigUrl);
                envKey = uri.Host.Split('.')[0];
            }

            // Read environment-specific configuration
            var envSection = configuration.GetSection($"Github:Environments:{envKey}");
            
            _canonicalHost = envSection["CanonicalHost"] ?? throw new InvalidOperationException(
                $"Github:Environments:{envKey}:CanonicalHost is not configured in appsettings.json");
            
            _vanityDomains = envSection.GetSection("VanityDomains").Get<string[]>() ?? Array.Empty<string>();
        }

        public string GetCanonicalHost() => _canonicalHost;

        public string[] GetVanityDomains() => _vanityDomains;

        public bool IsCanonicalHost(string host)
        {
            return string.Equals(host, _canonicalHost, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsVanityDomain(string host)
        {
            return _vanityDomains.Any(v => string.Equals(v, host, StringComparison.OrdinalIgnoreCase));
        }

        public string[] GetAllAllowedHosts()
        {
            return new[] { _canonicalHost }.Concat(_vanityDomains).ToArray();
        }
    }
}
