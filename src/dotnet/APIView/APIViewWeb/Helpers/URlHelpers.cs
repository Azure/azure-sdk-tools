
using System.Collections.Generic;

namespace APIViewWeb.Helpers
{
    public class URlHelpers
    {
        public static List<string> GetAllowedOrigins()
        {
            return new List<string>() {
                    // Production
                    "https://apiview.azurewebsites.net",
                    "https://spa.apiview.azurewebsites.net",
                    "https://apiview.dev",
                    "https://spa.apiview.dev",
                    // Staging
                    "https://apiviewstaging.azurewebsites.net",
                    "https://spa.apiviewstaging.azurewebsites.net",
                    "https://apiviewstagingtest.com",
                    "https://spa.apiviewstagingtest.com",
                    "https://apiview.org",
                    "https://spa.apiview.org",
                    // UX Test
                    "https://apiviewuat.azurewebsites.net",
                    "https://spa.apiviewuat.azurewebsites.net",
                    "https://apiviewuxtest.com",
                    "https://spa.apiviewuxtest.com"
                };
        }

        public static string[] GetAllowedProdOrigins()
        {
            return GetAllowedOrigins().ToArray();
        }

        public static string[] GetAllowedStagingOrigins()
        {
            var hosts = GetAllowedOrigins();
            hosts.Add("http://localhost:5000");
            return hosts.ToArray();
        }
    }
}
