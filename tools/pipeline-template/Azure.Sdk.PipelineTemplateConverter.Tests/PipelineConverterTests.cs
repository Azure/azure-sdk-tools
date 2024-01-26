using Xunit;
using FluentAssertions;

namespace Azure.Sdk.PipelineTemplateConverter.Tests;

public class PipelineConverterTests
{
    [Fact]
    public void TestGenerateStageTemplateNet()
    {
        PipelineTemplateConverter.Run(new FileInfo("./assets/net.archetype-sdk-client.before.yml"), true);
        var converted = File.ReadAllLines("./assets/net.archetype-sdk-client.before.yml");
        var after = File.ReadAllLines("./assets/net.archetype-sdk-client.after.yml");
        for (var i = 0; i < after.Length; i++)
        {
            after[i].Should().Be(converted[i]);
        }
    }

    [Fact]
    public void TestGenerateStageTemplateJs()
    {
        PipelineTemplateConverter.Run(new FileInfo("./assets/js.archetype-sdk-client.before.yml"), true);
        var converted = File.ReadAllLines("./assets/js.archetype-sdk-client.before.yml");
        var after = File.ReadAllLines("./assets/js.archetype-sdk-client.after.yml");
        for (var i = 0; i < after.Length; i++)
        {
            i.Should().BeLessThan(converted.Length);
            after[i].Should().Be(converted[i]);
        }
    }
}