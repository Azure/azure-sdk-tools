using YamlDotNet.Serialization;

namespace Azure.Sdk.PipelineTemplateConverter;

public class BaseTemplate
{
    [YamlMember(Alias = "resources", Order = 0)]
    public Dictionary<string, object>? Resources { get; set; }

    [YamlMember(Alias = "parameters", Order = 1)]
    public object? Parameters { get; set; }

    [YamlMember(Alias = "trigger", Order = 2)]
    public object? Trigger { get; set; }

    [YamlMember(Alias = "pr", Order = 3)]
    public object? PullRequest { get; set; }

    [YamlMember(Alias = "variables", Order = 4)]
    public object? Variables { get; set; }

    private ISerializer Serializer { get; } = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .DisableAliases()
        .WithIndentedSequences()
        .Build();

    public override string ToString()
    {
        return Serializer.Serialize(this) + Environment.NewLine;
    }
}
