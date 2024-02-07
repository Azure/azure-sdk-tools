using YamlDotNet.Serialization;
using YamlDotNet.Core;

namespace Azure.Sdk.PipelineTemplateConverter;

public class StageTemplate : BaseTemplate
{
    [YamlMember(Alias = "stages", ScalarStyle = ScalarStyle.Literal)]
    public List<Dictionary<string, object>>? Stages { get; set; }

    [YamlMember(Alias = "extends", Order = 11)]
    public Dictionary<string, object>? Extends { get; set; }

    [YamlMember(Alias = "pool", Order = 10)]
    public Dictionary<string, object>? Pool { get; set; }
}
