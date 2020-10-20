using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;

namespace CreateRuleFabricBot.Helpers
{
    public static class FileHelpers
    {
        public static string GetFileContents(string fileOrUri)
        {
            if (File.Exists(fileOrUri))
            {
                return File.ReadAllText(fileOrUri);
            }

            // try to parse it as an Uri
            Uri uri = new Uri(fileOrUri, UriKind.Absolute);
            if (uri.Scheme.ToLowerInvariant() != "https")
            {
                throw new ArgumentException("Cannot download off non-https uris");
            }

            // try to download it.
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = client.GetAsync(fileOrUri).ConfigureAwait(false).GetAwaiter().GetResult();
                return response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }
    }
}
