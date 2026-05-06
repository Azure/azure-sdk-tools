using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class SdkLanguageTests
{
    [TestCase(SdkLanguage.DotNet, "azure-sdk-for-net")]
    [TestCase(SdkLanguage.Go, "azure-sdk-for-go")]
    [TestCase(SdkLanguage.Java, "azure-sdk-for-java")]
    [TestCase(SdkLanguage.JavaScript, "azure-sdk-for-js")]
    [TestCase(SdkLanguage.Python, "azure-sdk-for-python")]
    [TestCase(SdkLanguage.Rust, "azure-sdk-for-rust")]
    [TestCase(SdkLanguage.Cpp, "azure-sdk-for-cpp")]
    public void GetRepoName_ReturnsExpectedRepoName(SdkLanguage language, string expected)
    {
        Assert.That(SdkLanguageHelpers.GetRepoName(language), Is.EqualTo(expected));
    }

    [Test]
    public void GetRepoName_Unknown_ReturnsNull()
    {
        Assert.That(SdkLanguageHelpers.GetRepoName(SdkLanguage.Unknown), Is.Null);
    }
}
