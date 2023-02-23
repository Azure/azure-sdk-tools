using Azure.Sdk.Tools.SecretRotation.Configuration;

namespace Azure.Sdk.Tools.SecretRotation.Tests.CoreTests;

public class PlanConfigurationTests
{
    [Test]
    public void FromFile_MissingFile_ThrowsException()
    {
        string configurationPath = TestFiles.ResolvePath("TestConfigurations/missing.json");

        Assert.Throws<RotationConfigurationException>(() => PlanConfiguration.FromFile(configurationPath));
    }

    [Test]
    public void FromFile_InvalidPath_ThrowsException()
    {
        string configurationPath = @"&invalid:path?";

        Assert.Throws<RotationConfigurationException>(() => PlanConfiguration.FromFile(configurationPath));
    }

    [Test]
    public void FromFile_ValidPath_ReturnConfiguration()
    {
        string configurationPath = TestFiles.ResolvePath("TestConfigurations/Valid/random-string.json");

        // Act
        PlanConfiguration configuration = PlanConfiguration.FromFile(configurationPath);

        Assert.NotNull(configuration);
    }
}
