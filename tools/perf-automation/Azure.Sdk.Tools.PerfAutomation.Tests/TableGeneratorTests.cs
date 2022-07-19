using System;
using Azure.Sdk.Tools.PerfAutomation.Models;
using NUnit.Framework;

namespace Azure.Sdk.Tools.PerfAutomation.Tests
{
    public class TableGeneratorTests
    {
        [Test]
        public void PartialRow()
        {
            var headers = new string[] { "h1", "h2", "h3" };
            var table = new string[][][]
            {
                new string[][]
                {
                    new string[] { "c1", "c2" }
                }
            };

            Console.WriteLine(TableGenerator.Generate(headers, table, OutputFormat.Txt));
        }
    }
}
