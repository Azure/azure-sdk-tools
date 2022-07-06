using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        }

        private async Task<string> GetResultsSummary(IEnumerable<Result> results)
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
