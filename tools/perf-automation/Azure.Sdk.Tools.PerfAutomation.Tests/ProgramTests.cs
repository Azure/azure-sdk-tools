using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Sdk.Tools.PerfAutomation.Models;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class ProgramTests
    {
        [Test]
        public async Task WriteResultsSummaryTwoVersionsNet()
        {
            Console.Write(await GetResultsSummary("two-versions-net.json"));
        }

        [Test]
        public async Task WriteResultsSummaryThreeVersionsNet()
        {
            Console.Write(await GetResultsSummary("three-versions-net.json"));
        }

        [Test]
        public async Task WriteResultsSummaryTwoVersionsJava()
        {
            Console.Write(await GetResultsSummary("two-versions-java.json"));
        }

        [Test]
        public async Task WriteResultsSummaryTwoVersionsJS()
        {
            Console.Write(await GetResultsSummary("two-versions-js.json"));
        }

        [Test]
        public async Task WriteResultsSummaryTwoVersionsPython()
        {
            Console.Write(await GetResultsSummary("two-versions-python.json"));
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
