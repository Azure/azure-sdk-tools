using Azure.Sdk.Tools.Cli.Models;

namespace Azure.Sdk.Tools.Cli.Tests.Models;

[TestFixture]
public class SdkLanguageTests
{
    [TestCase("DotNet")]
    [TestCase("Dotnet")]
    [TestCase("dotnet")]
    [TestCase("DOTNET")]
    [TestCase(".NET")]
    [TestCase(".net")]
    [TestCase("csharp")]
    [TestCase("c#")]
    public void GetSdkLanguage_DotNetAliasesPreserveWorkItemName(string alias)
    {
        var language = SdkLanguageHelpers.GetSdkLanguage(alias);

        Assert.That(language, Is.EqualTo(SdkLanguage.DotNet));
        Assert.That(language.ToWorkItemString(), Is.EqualTo(".NET"));
    }

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
