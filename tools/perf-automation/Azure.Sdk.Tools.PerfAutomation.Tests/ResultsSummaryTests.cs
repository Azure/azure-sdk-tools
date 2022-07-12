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

        [Test]
        public async Task Empty()
        {
            Console.Write(await GetResultsSummary(Enumerable.Empty<Result>()));
        }

        [Test]
        public async Task Net()
        {
            Console.Write(await GetResultsSummary("results-net.json"));
        }

        [Test]
        public async Task Java()
        {
            Console.Write(await GetResultsSummary("results-java.json"));
        }

        [Test]
        public async Task JS()
        {
            Console.Write(await GetResultsSummary("results-js.json"));
        }

        [Test]
        public async Task Python()
        {
            Console.Write(await GetResultsSummary("results-python.json"));
        }

        [Test]
        public async Task ThreeVersions()
        {
            Console.Write(await GetResultsSummary("results-three-versions.json"));
        }

        private static async Task<string> GetResultsSummary(string path)
        {
            List<Result> results;
            using (var stream = File.OpenRead(path))
            {
                results = await JsonSerializer.DeserializeAsync<List<Result>>(stream, options: Program.JsonOptions);
            }

            return await GetResultsSummary(results);
        }

        private static async Task<string> GetResultsSummary(IEnumerable<Result> results)
        {
            using var memoryStream = new MemoryStream();

            using (var streamWriter = new StreamWriter(memoryStream, leaveOpen: true))
            {
                await Program.WriteResultsSummary(streamWriter, results);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);

            using (var streamReader = new StreamReader(memoryStream))
            {
                return await streamReader.ReadToEndAsync();
            }               
        }
    }
}
