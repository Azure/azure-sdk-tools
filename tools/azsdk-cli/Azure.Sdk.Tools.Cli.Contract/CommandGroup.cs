using System.Collections.Generic;
using System.CommandLine;

namespace Azure.Sdk.Tools.Cli.Contract
{
    public record CommandGroup(
        string Verb,
        string Description,
        List<Option>? Options = null
    );
}
