using System;
using System.IO;
using System.Net.Http;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    public static class FileHelpers
    {
        public static string GetFileContents(string fileOrUri)
        {
            // try to parse it as an Uri
            if (fileOrUri.StartsWith("https"))
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = client.GetAsync(fileOrUri).ConfigureAwait(false).GetAwaiter().GetResult();
                    return response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }

            string fullPath = Path.GetFullPath(fileOrUri);
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }

            throw new ArgumentException($"The path provided is neither local path nor https link. Please check your path: {fileOrUri} resolved to {fullPath}.");
        }
    }
}
