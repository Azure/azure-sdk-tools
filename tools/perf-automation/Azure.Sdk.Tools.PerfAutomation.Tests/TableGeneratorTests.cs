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

        [Test]
        public void NoTrailingWhitespace()
        {
            var headers = new string[] { "h1", "h2", "h3" };
            var table = new string[][][]
            {
                new string[][]
                {
                    new string[] { "v1", "v2", "v3" }
                },
                new string[][]
                {
                    new string[] { "v4", "v5", "v6" }
                }
            };

            var text = TableGenerator.Generate(headers, table, OutputFormat.Txt);
            var lines = text.Split(Environment.NewLine);
            foreach (var line in lines)
            {
                Assert.AreEqual(line, line.TrimEnd());
            }
        }
    }
}
