using Azure.Sdk.Tools.SecretRotation.Configuration;

namespace Azure.Sdk.Tools.SecretRotation.Tests.CoreTests;

public class PlanConfigurationTests
{
    [Test]
    public void TryLoadFromFile_MissingFile_ThrowsException()
    {
        string configurationPath = TestFiles.ResolvePath("TestConfigurations/missing.json");

        Assert.Throws<RotationConfigurationException>(() => PlanConfiguration.TryLoadFromFile(configurationPath, out _));
    }

    [Test]
    public void TryLoadFromFile_InvalidPath_ThrowsException()
    {
        string configurationPath = @"&invalid:path?";

        Assert.Throws<RotationConfigurationException>(() => PlanConfiguration.TryLoadFromFile(configurationPath, out _));
    }

    [Test]
    public void TryLoadFromFile_WrongSchema_ReturnsFalse()
    {
        string configurationPath = TestFiles.ResolvePath("TestConfigurations/Invalid/wrong-schema.json");

        // Act
        bool success = PlanConfiguration.TryLoadFromFile(configurationPath, out var configuration);

        Assert.False(success);
        Assert.Null(configuration);
    }
    
    [Test]
    public void TryLoadFromFile_ValidPath_ReturnConfiguration()
    {
        string configurationPath = TestFiles.ResolvePath("TestConfigurations/Valid/random-string.json");

        // Act
        bool success = PlanConfiguration.TryLoadFromFile(configurationPath, out var configuration);

        Assert.True(success);
        Assert.NotNull(configuration);
    }
}
