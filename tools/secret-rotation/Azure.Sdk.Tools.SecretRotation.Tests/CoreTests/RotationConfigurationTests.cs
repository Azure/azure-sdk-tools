using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Tests.CoreTests;

public class RotationConfigurationTests
{
    [Test]
    public void LoadFrom_MissingFile_ThrowsException()
    {
        string missingPath = TestFiles.ResolvePath("TestConfigurations/missing.json");
        var storeFactories = new Dictionary<string, Func<StoreConfiguration, SecretStore>>();

        Assert.Throws<RotationConfigurationException>(() => RotationConfiguration.From(missingPath, storeFactories));
    }

    [Test]
    public void LoadFrom_InvalidPath_ThrowsException()
    {
        string invalidPath = @"&invalid:path?";
        var storeFactories = new Dictionary<string, Func<StoreConfiguration, SecretStore>>();

        Assert.Throws<RotationConfigurationException>(() => RotationConfiguration.From(invalidPath, storeFactories));
    }

    [Test]
    public void LoadFrom_ValidPath_ReturnConfiguration()
    {
        string validPath = TestFiles.ResolvePath("TestConfigurations/valid/random-string.json");
        var storeFactories = new Dictionary<string, Func<StoreConfiguration, SecretStore>>();

        // Act
        RotationConfiguration configuration = RotationConfiguration.From(validPath, storeFactories);

        Assert.NotNull(configuration);
    }

    [Test]
    public void GetPlan_ValidConfiguration_ReturnsPlan()
    {
        string configurationPath = TestFiles.ResolvePath("TestConfigurations/valid/random-string.json");

        var storeFactories = new Dictionary<string, Func<StoreConfiguration, SecretStore>>
        {
            ["Random String"] = _ => Mock.Of<SecretStore>(x => x.CanOriginate),
            ["Key Vault Secret"] = _ => Mock.Of<SecretStore>(x => x.CanWrite && x.CanRead)
        };

        RotationConfiguration configuration = RotationConfiguration.From(configurationPath, storeFactories);

        // Act
        RotationPlan? plan = configuration.GetRotationPlan("random-string", Mock.Of<ILogger>(), new TimeProvider());

        Assert.NotNull(plan);
    }
}
