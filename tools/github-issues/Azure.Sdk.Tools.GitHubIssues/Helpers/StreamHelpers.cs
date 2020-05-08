using System.IO;
using System.Text;

namespace GitHubIssues.Helpers
{
    internal static class StreamHelpers
    {
        public static Stream GetStreamForString(string s)
        {
            MemoryStream ms = new MemoryStream();
            byte[] content = Encoding.ASCII.GetBytes(s);
            ms.Write(content, 0, content.Length);

            ms.Position = 0;
            return ms;
        }

        public static string GetContentAsString(Stream s)
        {
            using StreamReader sr = new StreamReader(s);
            return sr.ReadToEnd();
        }
    }
}
