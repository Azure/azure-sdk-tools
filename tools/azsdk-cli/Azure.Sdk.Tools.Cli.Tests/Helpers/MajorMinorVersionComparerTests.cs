// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

[TestFixture]
public class MajorMinorVersionComparerTests
{
    // Compare tests: returns normalized -1, 0, or 1
    // Higher major wins
    [TestCase("2.0", "1.99", ExpectedResult = 1)]
    [TestCase("1.0", "0.99", ExpectedResult = 1)]
    [TestCase("10.0", "9.999", ExpectedResult = 1)]
    // Higher minor wins
    [TestCase("0.99", "0.1", ExpectedResult = 1)]
    [TestCase("1.10", "1.9", ExpectedResult = 1)]
    [TestCase("1.100", "1.99", ExpectedResult = 1)]
    // Equal versions
    [TestCase("1.0", "1.0", ExpectedResult = 0)]
    [TestCase("0.1", "0.1", ExpectedResult = 0)]
    [TestCase("99.99", "99.99", ExpectedResult = 0)]
    // Lower version returns negative
    [TestCase("1.99", "2.0", ExpectedResult = -1)]
    [TestCase("0.99", "1.0", ExpectedResult = -1)]
    [TestCase("0.1", "0.99", ExpectedResult = -1)]
    public int Compare_ValidVersions_ReturnsExpectedResult(string x, string y)
    {
        int result = MajorMinorVersionComparer.Default.Compare(x, y);
        return result < 0 ? -1 : (result > 0 ? 1 : 0);
    }

    // ParseVersion valid tests
    [TestCase("1.2", 1, 2)]
    [TestCase("999.1000", 999, 1000)]
    [TestCase("0.0", 0, 0)]
    [TestCase("0.99", 0, 99)]
    public void ParseVersion_ValidFormat_ReturnsExpectedResult(string version, int expectedMajor, int expectedMinor)
    {
        var (major, minor) = MajorMinorVersionComparer.ParseVersion(version);

        Assert.Multiple(() =>
        {
            Assert.That(major, Is.EqualTo(expectedMajor));
            Assert.That(minor, Is.EqualTo(expectedMinor));
        });
    }

    // ParseVersion invalid format throws FormatException
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("1")]           // Not enough parts
    [TestCase("1.2.3")]       // Too many parts
    [TestCase("a.b")]         // Non-numeric
    [TestCase("invalid")]
    public void ParseVersion_InvalidFormat_ThrowsFormatException(string? version)
    {
        Assert.Throws<FormatException>(() => MajorMinorVersionComparer.ParseVersion(version));
    }
}
