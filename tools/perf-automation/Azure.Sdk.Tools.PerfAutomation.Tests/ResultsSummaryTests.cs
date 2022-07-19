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
    public class ResultsSummaryTests
    {
        // Tests generate results summaries from sample results.json files.  Output is printed to console
        // for manual inspection in Test Explorer, rather than asserting specific output, since it will
        // change frequently.

        [TestCase(OutputFormat.Csv)]
        [TestCase(OutputFormat.Md)]
        [TestCase(OutputFormat.Txt)]
        public async Task Empty(OutputFormat outputFormat)
        {
            Console.Write(await GetResultsSummary(Enumerable.Empty<Result>(), outputFormat));
        }

        [TestCase("results/no-iterations.json", OutputFormat.Csv)]
        [TestCase("results/no-iterations.json", OutputFormat.Md)]
        [TestCase("results/no-iterations.json", OutputFormat.Txt)]
        [TestCase("results/partial-rows.json", OutputFormat.Csv)]
        [TestCase("results/partial-rows.json", OutputFormat.Md)]
        [TestCase("results/partial-rows.json", OutputFormat.Txt)]
        [TestCase("results/java.json", OutputFormat.Csv)]
        [TestCase("results/java.json", OutputFormat.Md)]
        [TestCase("results/java.json", OutputFormat.Txt)]
        [TestCase("results/net.json", OutputFormat.Csv)]
        [TestCase("results/net.json", OutputFormat.Md)]
        [TestCase("results/net.json", OutputFormat.Txt)]
        [TestCase("results/js.json", OutputFormat.Csv)]
        [TestCase("results/js.json", OutputFormat.Md)]
        [TestCase("results/js.json", OutputFormat.Txt)]
        [TestCase("results/python.json", OutputFormat.Csv)]
        [TestCase("results/python.json", OutputFormat.Md)]
        [TestCase("results/python.json", OutputFormat.Txt)]
        [TestCase("results/three-versions.json", OutputFormat.Csv)]
        [TestCase("results/three-versions.json", OutputFormat.Md)]
        [TestCase("results/three-versions.json", OutputFormat.Txt)]
        public async Task FromFile(string fileName, OutputFormat outputFormat)
        {
            Console.Write(await GetResultsSummary(fileName, outputFormat));
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
