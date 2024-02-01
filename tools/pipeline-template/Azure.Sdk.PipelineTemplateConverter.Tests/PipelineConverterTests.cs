using Xunit;
using FluentAssertions;

namespace Azure.Sdk.PipelineTemplateConverter.Tests;

public class PipelineConverterTests
{
    [Fact]
    public void TestGenerateStageTemplateNet()
    {
        File.Copy("./assets/net.archetype-sdk-client.before.yml", "./assets/net.archetype-sdk-client.converted.yml", true);
        PipelineTemplateConverter.Convert(new FileInfo("./assets/net.archetype-sdk-client.converted.yml"), true);
        var converted = File.ReadAllLines("./assets/net.archetype-sdk-client.converted.yml");
        var after = File.ReadAllLines("./assets/net.archetype-sdk-client.after.yml");
        for (var i = 0; i < after.Length; i++)
        {
            after[i].Should().Be(converted[i]);
        }
    }

    [Fact]
    public void TestGenerateStageTemplateJs()
    {
        File.Copy("./assets/js.archetype-sdk-client.before.yml", "./assets/js.archetype-sdk-client.converted.yml", true);
        PipelineTemplateConverter.Convert(new FileInfo("./assets/js.archetype-sdk-client.converted.yml"), true);
        var converted = File.ReadAllLines("./assets/js.archetype-sdk-client.converted.yml");
        var after = File.ReadAllLines("./assets/js.archetype-sdk-client.after.yml");
        for (var i = 0; i < after.Length; i++)
        {
            i.Should().BeLessThan(converted.Length);
            after[i].Should().Be(converted[i]);
        }
    }

    [Fact]
    public void TestGetTemplateType()
    {
        var contents = File.ReadAllText("./assets/net.archetype-sdk-client.before.yml");
        var templateType = PipelineTemplateConverter.GetTemplateType(contents);
        templateType.Should().Equal(new List<TemplateType>{ TemplateType.Stage });

        contents = File.ReadAllText("./assets/net.aggregate-reports.before.yml");
        templateType = PipelineTemplateConverter.GetTemplateType(contents);
        templateType.Should().Equal(new List<TemplateType>{ TemplateType.Stage, TemplateType.ArtifactTask });
    }

    [Fact]
    public void TestFixTemplateSpecialCharacters()
    {
        var contents = @"
extends:
  ""${{ if eq(variables['System.TeamProject'], 'internal') }}:"":
    template: v1/1ES.Unofficial.PipelineTemplate.yml@1ESPipelineTemplates
  '${{ else }}:':
    template: stage-redirect.yml
  parameters:
";

        var expected = @"
extends:
  ${{ if eq(variables['System.TeamProject'], 'internal') }}:
    template: v1/1ES.Unofficial.PipelineTemplate.yml@1ESPipelineTemplates
  ${{ else }}:
    template: stage-redirect.yml
  parameters:
";

        var template = PipelineTemplateConverter.FixTemplateSpecialCharacters(contents);
        template.Should().Be(expected);
    }
}