using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Utils;
using Azure.Sdk.Tools.CodeownersUtils.Constants;
using NUnit.Framework;

namespace Azure.Sdk.Tools.CodeownersUtils.Tests.Utils
{
    [TestFixture]
    [Parallelizable(ParallelScope.Children)]
    // All of these tests can run in parallel
    public class MonikerUtilsTests
    {
        [Category("Utils")]
        [Category("Moniker")]
        // These should be using the SeparatorConstants.Colon but the interpolated strings doesn't seem to want to accept this
        [TestCase(MonikerConstants.AzureSdkOwners, $"# {MonikerConstants.AzureSdkOwners}:          @fakeOwner1 @fakeOwner2")]
        [TestCase(MonikerConstants.MissingFolder, $"#{MonikerConstants.MissingFolder}     @fakeOwner1 @fakeOwner2")]
        [TestCase(MonikerConstants.PRLabel, $"# {MonikerConstants.PRLabel}: %Fake Label")]
        [TestCase(MonikerConstants.ServiceLabel, $"# {MonikerConstants.ServiceLabel}: %Fake Label")]
        [TestCase(MonikerConstants.ServiceOwners, $"# {MonikerConstants.ServiceOwners}:")]
        // Moniker only lines, without their owners or labels, still need to be positively identified
        [TestCase(MonikerConstants.AzureSdkOwners, $"# {MonikerConstants.AzureSdkOwners}:")]
        [TestCase(MonikerConstants.MissingFolder, $"#{MonikerConstants.MissingFolder}")]
        [TestCase(MonikerConstants.PRLabel, $"# {MonikerConstants.PRLabel}:")]
        [TestCase(MonikerConstants.ServiceLabel, $"# {MonikerConstants.ServiceLabel}:")]
        [TestCase(MonikerConstants.ServiceOwners, $"# {MonikerConstants.ServiceOwners}:")]
        public void TestMonikerParsingForMonikerLines(string moniker, string line) 
        {
            // The MonikerUtils has 2 methods to test for Moniker parsing
            // 1. ParseMonikerFromLine - returns the moniker if one is found on the line
            // 2. IsMonikerLine - returns true if the line is a moniker line
            bool isMonikerLine = MonikerUtils.IsMonikerLine(line);
            Assert.IsTrue(isMonikerLine, $"IsMonikerLine for '{line}' contains '{moniker}' and should have returned true.");
            string parsedMoniker = MonikerUtils.ParseMonikerFromLine(line);
            Assert.That(parsedMoniker, Is.EqualTo(moniker), $"ParseMonikerFromLine for '{line}' should have returned '{moniker}' but returned '{parsedMoniker}'");
        }

        [Category("Utils")]
        [Category("Moniker")]
        [TestCase("# just a comment line")]
        // Whitespace line with spaces and tabs
        [TestCase("  \t")]
        [TestCase("")]
        public void TestMonikerParsingForNonMonikerLines(string sourceLine)
        {
            bool isMonikerLine = MonikerUtils.IsMonikerLine(sourceLine);
            Assert.IsFalse(isMonikerLine, $"IsMonikerLine for '{sourceLine}' does not contain a moniker and should have returned false.");
            string parsedMoniker = MonikerUtils.ParseMonikerFromLine(sourceLine);
            Assert.That(parsedMoniker, Is.EqualTo(null), $"ParseMonikerFromLine for '{sourceLine}' should have returned 'null' but returned '{parsedMoniker}'");
        }

        [Category("Utils")]
        [Category("Moniker")]
        // Source line with owners
        [TestCase("/fakePath1/fakePath2    @fakeOwner1  @fakeOwner2")]
        // Source line with no owners
        [TestCase("/fakePath1/fakePath2")]
        public void TestIsMonikerOrSourceLineForSourceLines(string sourceLine)
        {
            bool isMonikerLine = MonikerUtils.IsMonikerLine(sourceLine);
            Assert.IsFalse(isMonikerLine, $"IsMonikerLine for '{sourceLine}' should have returned false for a source line.");
            string parsedMoniker = MonikerUtils.ParseMonikerFromLine(sourceLine);
            Assert.That(parsedMoniker, Is.EqualTo(null), $"ParseMonikerFromLine for '{sourceLine}' should have returned 'null' for a source line but returned '{parsedMoniker}'");
        }
    }
}
