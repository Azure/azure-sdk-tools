using System.Linq;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using Xunit;

namespace APIViewUnitTests;

public class LanguageExpansionRulesTests
{
    #region PackageKey Tests

    [Theory]
    [InlineData("Python", "",   "Python")]
    [InlineData("Java",   "v2", "Java:v2")]
    public void PackageKey_ToString(string language, string flavor, string expected)
        => Assert.Equal(expected, new PackageKey(language, flavor).ToString());

    [Theory]
    [InlineData("Java",    "Java", "",   false)]
    [InlineData("Java:v2", "Java", "v2", true)]
    public void PackageKey_Parse(string input, string expectedLanguage, string expectedFlavor, bool hasFlavor)
    {
        var key = PackageKey.Parse(input);
        Assert.Equal(expectedLanguage, key.Language);
        Assert.Equal(expectedFlavor,   key.Flavor);
        Assert.Equal(hasFlavor,        key.HasFlavor);
    }

    [Fact]
    public void PackageKey_Parse_RoundTrips()
        => Assert.Equal("Java:v2", PackageKey.Parse("Java:v2").ToString());

    #endregion

    #region DefaultExpansionRule Tests

    [Fact]
    public void DefaultExpansionRule_Expand_ProducesOneEntryKeyedByLanguage()
    {
        var config = new LanguageConfig { PackageName = "azure-core", Namespace = "azure.core", Flavor = "azure" };
        var result = DefaultExpansionRule.Instance.Expand("Python", config).ToList();

        Assert.Single(result);
        Assert.Equal("Python",     result[0].Item1);
        Assert.Equal("azure-core", result[0].Item2.PackageName);
        Assert.Equal("azure.core", result[0].Item2.Namespace);
    }

    [Fact]
    public void DefaultExpansionRule_Expand_FlavorIsIgnored_KeyIsPlainLanguage()
    {
        // Flavor="azure" is the default for most languages — must not bleed into the key
        var config = new LanguageConfig { PackageName = "pkg", Namespace = "ns", Flavor = "azure" };
        var result = DefaultExpansionRule.Instance.Expand("JavaScript", config).ToList();

        Assert.Single(result);
        Assert.Equal("JavaScript", result[0].Item1);   // NOT "JavaScript:azure"
    }

    [Theory]
    [InlineData("Python",     "azure-core",  "Python")]
    [InlineData("JavaScript", "@azure/core", "JavaScript")]
    public void DefaultExpansionRule_GetLanguageKey_ReturnsNormalizedLanguage(string language, string packageName, string expected)
        => Assert.Equal(expected, DefaultExpansionRule.Instance.GetLanguageKey(language, packageName));

    #endregion

    #region JavaExpansionRule 

    [Fact]
    public void JavaExpansionRule_Expand_NonV2Flavor_ProducesOneJavaEntry()
    {
        var config = new LanguageConfig
        {
            PackageName = "com.azure:azure-core",
            Namespace = "com.azure.core",
            Flavor = "azure"   // default flavor, NOT v2
        };

        var result = JavaExpansionRule.Instance.Expand("Java", config).ToList();

        Assert.Single(result);
        Assert.Equal("Java",                 result[0].Item1);
        Assert.Equal("com.azure:azure-core", result[0].Item2.PackageName);
        Assert.Equal("com.azure.core",       result[0].Item2.Namespace);
    }

    [Fact]
    public void JavaExpansionRule_Expand_V2Flavor_ProducesTwoEntries()
    {
        var config = new LanguageConfig
        {
            PackageName = "com.azure.v2:azure-core-test",
            Namespace = "com.azure.v2.core",
            Flavor = "azurev2"
        };

        var result = JavaExpansionRule.Instance.Expand("Java", config).ToList();

        Assert.Equal(2, result.Count);

        var v2Entry   = result.Single(r => r.Item1 == "Java:v2");
        Assert.Equal("com.azure.v2:azure-core-test", v2Entry.Item2.PackageName);
        Assert.Equal("com.azure.v2.core",            v2Entry.Item2.Namespace);

        var baseEntry = result.Single(r => r.Item1 == "Java");
        Assert.Equal("com.azure:azure-core-test", baseEntry.Item2.PackageName);
        Assert.Equal("com.azure.core",            baseEntry.Item2.Namespace);
    }

    [Fact]
    public void JavaExpansionRule_Expand_V2_PackageNameStripping_RemovesDotV2Group()
    {
        var config = new LanguageConfig
        {
            PackageName = "com.azure.v2:azure-security-keyvault-keys",
            Namespace = "com.azure.v2.security.keyvault.keys",
            Flavor = "azurev2"
        };

        var result    = JavaExpansionRule.Instance.Expand("Java", config).ToList();
        var baseEntry = result.Single(r => r.Item1 == "Java");

        Assert.Equal("com.azure:azure-security-keyvault-keys", baseEntry.Item2.PackageName);
        Assert.Equal("com.azure.security.keyvault.keys",       baseEntry.Item2.Namespace);
    }

    [Theory]
    [InlineData("com.azure.v2:azure-core-test",          "Java:v2")]  // .v2: present → v2
    [InlineData("com.azure:azure-core",                  "Java")]     // no .v2: → base
    [InlineData("com.azure.v2.security.keyvault.keys",   "Java")]     // .v2. without colon → base
    [InlineData("com.azure.v2beta:azure-core",           "Java")]     // v2beta must not match
    public void JavaExpansionRule_GetLanguageKey(string packageName, string expectedKey)
        => Assert.Equal(expectedKey, JavaExpansionRule.Instance.GetLanguageKey("Java", packageName));

    #endregion
}
