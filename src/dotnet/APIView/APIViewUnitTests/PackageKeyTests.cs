using APIViewWeb.Models;
using Xunit;

namespace APIViewUnitTests;

public class PackageKeyTests
{
    [Theory]
    [InlineData("Java", "azurev2", "Java:azurev2")]
    [InlineData("Java", "azure", "Java:azure")]
    [InlineData("Python", "azure", "Python:azure")]
    public void PackageKey_ToString(string language, string flavor, string expected)
        => Assert.Equal(expected, new PackageKey(language, flavor).ToString());

    [Theory]
    [InlineData("Java:azurev2", "Java", "azurev2")]
    [InlineData("Java:azure", "Java", "azure")]
    [InlineData("Python:azure", "Python", "")]
    public void PackageKey_Parse(string input, string expectedLanguage, string expectedFlavor)
    {
        var key = PackageKey.Parse(input);
        Assert.Equal(expectedLanguage, key.Language);
        Assert.Equal(expectedFlavor, key.Flavor);
    }
}
