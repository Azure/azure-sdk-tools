using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PerfAutomation.Models;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class NetTests
    {
        [Test]
        public void GetRuntimePackageVersions()
        {
            var standardOutput = File.ReadAllText("stdout/net.txt");

            var expected = new Dictionary<string, string>() {
                { "Azure.Core", "1.25.0+c8aaee521e662ddfb238d5ad1f2f9a79233f97f6" }
            };

            var actual = Net.GetRuntimePackageVersions(standardOutput);

            CollectionAssert.AreEquivalent(expected, actual);
        }

        private static async Task<string> GetResultsSummary(string path, OutputFormat outputFormat)
        {
            List<Result> results;
            using (var stream = File.OpenRead(path))
            {
                results = await JsonSerializer.DeserializeAsync<List<Result>>(stream, options: Program.JsonOptions);
            }

            return await GetResultsSummary(results, outputFormat);
        }

        private static async Task<string> GetResultsSummary(IEnumerable<Result> results, OutputFormat outputFormat)
        {
            using var memoryStream = new MemoryStream();

            using (var streamWriter = new StreamWriter(memoryStream, leaveOpen: true))
            {
                await Program.WriteResultsSummary(streamWriter, results, outputFormat);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);

            using (var streamReader = new StreamReader(memoryStream))
            {
                return await streamReader.ReadToEndAsync();
            }
        }
    }
}
