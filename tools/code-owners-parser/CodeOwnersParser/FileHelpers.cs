using System;
using System.IO;
using System.Net.Http;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    public static class FileHelpers
    {
        public static string GetFileContents(string fileOrUri)
        {
            string fullPath = (new DirectoryInfo(fileOrUri)).FullName;
            // try to parse it as an Uri
            if (fullPath.StartsWith("https"))
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = client.GetAsync(fileOrUri).ConfigureAwait(false).GetAwaiter().GetResult();
                    return response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }
            if (File.Exists(fullPath))
            {
                return File.ReadAllText(fullPath);
            }

            throw new ArgumentException(string.Format("The path provided is neither local path nor https link. Please check your path: {0}", fileOrUri));
        }
    }
}
