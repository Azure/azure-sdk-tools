
using System.Collections.Generic;

namespace APIViewWeb.Helpers
{
    public class URlHelpers
    {
        public static List<string> GetAllowedOrigins()
        {
            return new List<string>() {
                    "https://spa.apiviewuxtest.com",
                    "https://spa.apiviewstagingtest.com",
                    "https://spa.apiview.org"
                };
        }

        public static string[] GetAllowedProdOrigins()
        {
            return GetAllowedOrigins().ToArray();
        }

        public static string[] GetAllowedStagingOrigins()
        {
            var hosts = GetAllowedOrigins();
            hosts.Add("https://localhost:4200");
            return hosts.ToArray();
        }
    }
}
