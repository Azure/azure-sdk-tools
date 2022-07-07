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
        public async Task WriteResultsSummary()
        {
            List<Result> results;
            using (var stream = File.OpenRead("results.json")) {
                results = await JsonSerializer.DeserializeAsync<List<Result>>(stream, options: Program.JsonOptions);
            }

            var resultsSummary = await GetResultsSummary(results);
            Console.WriteLine(resultsSummary);
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
