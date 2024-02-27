using YamlDotNet.Serialization;
using YamlDotNet.Core;

namespace Azure.Sdk.PipelineTemplateConverter;

public class JobTemplate : BaseTemplate
{
    [YamlMember(Alias = "jobs", Order = 10)]
    public List<Dictionary<string, object>>? Jobs { get; set; }

    public override string ToString()
    {
        var output = base.ToString();
        output = output.Replace(@"AgentImage: $(OSVmImage)", @"AgentImage: ${{ parameters.OSName }}");
        output = output.Replace(@"$(OSName)", @"${{ parameters.OSName }}");
        return output;
    }
}