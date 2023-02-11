using System;
using System.IO;
using System.Net.Http;

namespace Azure.Sdk.Tools.CodeOwnersParser
{
    public static class FileHelpers
    {
        public static string GetFileOrUrlContents(string fileOrUrl)
        {
            if (fileOrUrl.StartsWith("https"))
                return GetUrlContents(fileOrUrl);

            string fullPath = Path.GetFullPath(fileOrUrl);
            if (File.Exists(fullPath))
                return File.ReadAllText(fullPath);

            throw new ArgumentException(
                "The path provided is neither local path nor https link. " +
                $"Please check your path: '{fileOrUrl}' resolved to '{fullPath}'.");
        }

        private static string GetUrlContents(string url)
        {
            using HttpClient client = new HttpClient();
            HttpResponseMessage response =
                client.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
            return response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
