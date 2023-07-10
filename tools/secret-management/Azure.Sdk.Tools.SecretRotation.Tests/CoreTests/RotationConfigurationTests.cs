using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.SecretRotation.Tests.CoreTests;

public class RotationConfigurationTests
{
    [Test]
    public void From_MissingFile_ThrowsException()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/Valid");
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new ();
        string[] names = { "does-not-match-anything" };
        string[] tags = { };

        Assert.Throws<RotationConfigurationException>(() => RotationConfiguration
            .From(names, tags, configRoot, storeFactories));
    }

    [Test]
    public void From_MissingTags_ThrowsException()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/Valid");
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new();
        string[] names = { };
        string[] tags = { "does-not-match-anything" };

        Assert.Throws<RotationConfigurationException>(() => RotationConfiguration
            .From(names, tags, configRoot, storeFactories));
    }

    [Test]
    public void From_InvalidConfigPath_ThrowsException()
    {
        string configRoot = "&invalid:path?";
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new();
        string[] names = { };
        string[] tags = { };

        Assert.Throws<RotationConfigurationException>(() => RotationConfiguration
            .From(names, tags, configRoot, storeFactories));
    }

    [Test]
    public void From_ValidPath_ReturnConfiguration()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/Valid");
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new();
        string[] names = { "random-string" };
        string[] tags = { };

        // Act
        RotationConfiguration configuration = RotationConfiguration.From(names, tags, configRoot, storeFactories);

        Assert.NotNull(configuration);
    }

    [Test]
    public void From_NoNamesOrTags_LoadsAllConfigs()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/TagMatching");
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new();
        string[] names = { };
        string[] tags = { };

        // Act
        RotationConfiguration configuration = RotationConfiguration.From(names, tags, configRoot, storeFactories);

        // Assert
        string[] planNames = configuration.PlanConfigurations.Select(plan => plan.Name).ToArray();

        Assert.That(planNames, Is.EquivalentTo(new[] { "one", "two", "three" , "four", "five", "six" }));
    }

    [Test]
    public void From_Name_LoadsMatchingConfigs()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/TagMatching");
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new();
        string[] names = { "five" };
        string[] tags = { };

        // Act
        RotationConfiguration configuration = RotationConfiguration.From(names, tags, configRoot, storeFactories);

        // Assert
        string[] planNames = configuration.PlanConfigurations.Select(plan => plan.Name).ToArray();

        Assert.That(planNames, Is.EquivalentTo(new[] { "five" }));
    }

    [Test]
    public void From_Names_LoadsMatchingConfigs()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/TagMatching");
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new();
        string[] names = { "five", "six" };
        string[] tags = { };

        // Act
        RotationConfiguration configuration = RotationConfiguration.From(names, tags, configRoot, storeFactories);

        // Assert
        string[] planNames = configuration.PlanConfigurations.Select(plan => plan.Name).ToArray();

        Assert.That(planNames, Is.EquivalentTo(new[] { "five", "six" }));
    }

    [Test]
    public void From_Tags_LoadsMatchingConfigs()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/TagMatching");
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new();
        string[] names = { };
        string[] tags = { "even" };

        // Act
        RotationConfiguration configuration = RotationConfiguration.From(names, tags, configRoot, storeFactories);

        // Assert
        string[] planNames = configuration.PlanConfigurations.Select(plan => plan.Name).ToArray();

        Assert.That(planNames, Is.EquivalentTo(new[] { "two", "four", "six" }));
    }

    [Test]
    public void From_NameAndTags_LoadsMatchingConfigs()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/TagMatching");
        Dictionary<string, Func<StoreConfiguration, SecretStore>> storeFactories = new();
        string[] names = { "four" };
        string[] tags = { "even" };

        // Act
        RotationConfiguration configuration = RotationConfiguration.From(names, tags, configRoot, storeFactories);

        // Assert
        string[] planNames = configuration.PlanConfigurations.Select(plan => plan.Name).ToArray();

        Assert.That(planNames, Is.EquivalentTo(new[] { "four" }));
    }

    [Test]
    public void GetAllRotationPlans_ValidConfiguration_ReturnsPlan()
    {
        string configRoot = TestFiles.ResolvePath("TestConfigurations/Valid");

        var storeFactories = new Dictionary<string, Func<StoreConfiguration, SecretStore>>
        {
            ["Random String"] = _ => Mock.Of<SecretStore>(x => x.CanOriginate),
            ["Key Vault Secret"] = _ => Mock.Of<SecretStore>(x => x.CanWrite && x.CanRead)
        };

        string[] names = { "random-string" };
        string[] tags = { };

        RotationConfiguration configuration = RotationConfiguration.From(names, tags, configRoot, storeFactories);

        // Act
        IEnumerable<RotationPlan> plans = configuration.GetAllRotationPlans(Mock.Of<ILogger>(), new TimeProvider());

        Assert.AreEqual(1, plans.Count());
    }
}
