using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    /// <summary>
    /// Utility class for loading files from paths or URLs.
    /// </summary>
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

        /// <summary>
        /// Load a file from the repository into an List&lt;string&gt;. 
        /// Q) Why is this necessary?
        /// A) There are some pieces of metadata that require looking forward to ensure correctness.
        /// </summary>
        /// <param name="fileOrUrl">The file path or URL of the file</param>
        /// <returns>A List&lt;string&gt; representing the file</returns>
        public static List<string> LoadFileAsStringList(string fileOrUrl)
        {
            List<string> codeownersFileAsList = new List<string>();
            // GetFileOrUrlContents will throw
            string content = GetFileOrUrlContents(fileOrUrl);
            using StringReader sr = new StringReader(content);
            while (sr.ReadLine() is { } line)
            {
                codeownersFileAsList.Add(line);
            }

            return codeownersFileAsList;
        }

        private static string GetUrlContents(string url)
        {
            int maxRetries = 3;
            int attempts = 1;
            int delayTimeInMs = 1000;
            using HttpClient client = new HttpClient();
            while (attempts <= maxRetries)
            {
                try
                {
                    HttpResponseMessage response = client.GetAsync(url).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        // This writeline is probably unnecessary but good to have if there are previous attempts that failed
                        Console.WriteLine($"GetUrlContents for {url} attempt number {attempts} succeeded.");
                        return response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    else
                    {
                        Console.WriteLine($"GetUrlContents attempt number {attempts}. Non-{HttpStatusCode.OK} status code trying to fetch {url}. Status Code = {response.StatusCode}");
                    }
                }
                catch (HttpRequestException httpReqEx)
                {
                    // HttpRequestException means the request failed due to an underlying issue such as network connectivity,
                    // DNS failure, server certificate validation or timeout.
                    Console.WriteLine($"GetUrlContents attempt number {attempts}. HttpRequestException trying to fetch {url}. Exception message = {httpReqEx.Message}");
                    if (attempts == maxRetries)
                    {
                        // At this point the retries have been exhausted, let this rethrow
                        throw;
                    }
                }
                System.Threading.Thread.Sleep(delayTimeInMs);
                attempts++;
            }
            // This will only get hit if the final retry is non-OK status code
            throw new FileLoadException($"Unable to fetch {url} after {maxRetries}. See above for status codes for each attempt.");
        }
    }
}
